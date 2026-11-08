using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OSService.Api.Controllers;
using OSService.Application.Commands;
using OSService.Application.Saga;
using OSService.Application.Services;
using OSService.Domain.Entities;
using OSService.Domain.Enum;
using OSService.Domain.Interface;
using OSService.Infrastructure.Contexts;
using OSService.Infrastructure.Messaging;
using OSService.Infrastructure.Repositories;
using Reqnroll;
using Xunit;

namespace OSService.Tests.BDD.Steps;

[Binding]
public sealed class ServiceOrderLifecycleSteps : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceOrderDbContext _dbContext;
    private readonly ServiceOrderRepository _repository;
    private readonly OutboxRepository _outboxRepository;
    private readonly ServiceProvider _outboxServiceProvider;
    private readonly OutboxPublisherBackgroundService _outboxPublisher;
    private readonly Mock<IServiceOrderNotificationPublisher> _notificationPublisherMock;
    private readonly Mock<IServiceOrderMetrics> _metricsMock;
    private readonly Mock<ISagaEventPublisher> _sagaPublisherMock;
    private readonly List<string> _publishedSagaEvents = new();
    private readonly ServiceOrderService _serviceOrderService;
    private readonly SagaEventProcessor _sagaEventProcessor;
    private readonly ServiceOrderController _controller;

    private Guid _customerId;
    private Guid _vehicleId;
    private Guid _serviceOrderId;
    private IActionResult? _lastActionResult;
    private ServiceOrderGetResponse? _lastResponse;

    public ServiceOrderLifecycleSteps()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ServiceOrderDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new ServiceOrderDbContext(options);
        _dbContext.Database.EnsureCreated();

        _repository = new ServiceOrderRepository(_dbContext);
        _outboxRepository = new OutboxRepository(_dbContext);
        _notificationPublisherMock = new Mock<IServiceOrderNotificationPublisher>();
        _metricsMock = new Mock<IServiceOrderMetrics>();
        _sagaPublisherMock = new Mock<ISagaEventPublisher>();

        _sagaPublisherMock
            .Setup(x => x.PublishRawAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, CancellationToken>((eventType, _, _, _) =>
            {
                _publishedSagaEvents.Add(eventType);
            })
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddScoped<IOutboxRepository>(_ => _outboxRepository);
        services.AddSingleton(_sagaPublisherMock.Object);
        _outboxServiceProvider = services.BuildServiceProvider();

        _outboxPublisher = new OutboxPublisherBackgroundService(
            _outboxServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OutboxPublisherBackgroundService>.Instance);

        _serviceOrderService = new ServiceOrderService(
            _repository,
            _notificationPublisherMock.Object,
            NullLogger<ServiceOrderService>.Instance,
            _metricsMock.Object);

        _sagaEventProcessor = new SagaEventProcessor(
            _repository,
            _notificationPublisherMock.Object,
            _metricsMock.Object,
            NullLogger<SagaEventProcessor>.Instance);

        _controller = new ServiceOrderController(_serviceOrderService);
    }

    [Given(@"a valid customer and their vehicle are registered for opening a Service Order")]
    public void GivenAValidCustomerAndTheirVehicleAreRegisteredForOpeningAServiceOrder()
    {
        _customerId = Guid.NewGuid();
        _vehicleId = Guid.NewGuid();
    }

    [Given(@"a Service Order is in progress with an approved budget of (.*)")]
    public async Task GivenAServiceOrderIsInProgressWithAnApprovedBudgetOf(string budgetValue)
    {
        var budget = decimal.Parse(budgetValue, CultureInfo.InvariantCulture);

        GivenAValidCustomerAndTheirVehicleAreRegisteredForOpeningAServiceOrder();
        await WhenANewServiceOrderIsOpenedForTheCustomerAndVehicle();
        await WhenTheMechanicStartsDiagnosticsOnTheServiceOrder();
        await WhenTheMechanicFinishesTheTechnicalAnalysisWithABudgetOf(budget.ToString(CultureInfo.InvariantCulture));
        await WhenTheCustomerApprovesTheServiceOrderBudget();
        await WhenTheSagaReceivesAPaymentConfirmedEventWithTransaction("BDD-APPROVED-TX");
    }

    [When(@"a new Service Order is opened for the customer and vehicle")]
    public async Task WhenANewServiceOrderIsOpenedForTheCustomerAndVehicle()
    {
        var request = new ServiceOrderRequest
        {
            CustomerId = _customerId,
            VehicleId = _vehicleId
        };

        _lastActionResult = await _controller.Create(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(_lastActionResult);
        _lastResponse = Assert.IsType<ServiceOrderGetResponse>(createdResult.Value);
        _serviceOrderId = _lastResponse.Id;
    }

    [When(@"the mechanic starts diagnostics on the Service Order")]
    public async Task WhenTheMechanicStartsDiagnosticsOnTheServiceOrder()
    {
        _lastActionResult = await _controller.StartDiagnostics(_serviceOrderId);
        var okResult = Assert.IsType<OkObjectResult>(_lastActionResult);
        _lastResponse = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
    }

    [When(@"the mechanic finishes the technical analysis with a budget of (.*)")]
    public async Task WhenTheMechanicFinishesTheTechnicalAnalysisWithABudgetOf(string budgetValue)
    {
        var budget = decimal.Parse(budgetValue, CultureInfo.InvariantCulture);

        var request = new ServiceOrderFinishAnalysisRequest
        {
            Budget = budget
        };

        _lastActionResult = await _controller.FinishAnalysis(_serviceOrderId, request);
        var okResult = Assert.IsType<OkObjectResult>(_lastActionResult);
        _lastResponse = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
    }

    [When(@"the customer approves the Service Order budget")]
    public async Task WhenTheCustomerApprovesTheServiceOrderBudget()
    {
        _lastActionResult = await _controller.Approve(_serviceOrderId);
        var okResult = Assert.IsType<OkObjectResult>(_lastActionResult);
        _lastResponse = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
    }

    [When(@"the customer rejects the Service Order budget")]
    public async Task WhenTheCustomerRejectsTheServiceOrderBudget()
    {
        _lastActionResult = await _controller.Reject(_serviceOrderId);
        var okResult = Assert.IsType<OkObjectResult>(_lastActionResult);
        _lastResponse = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
    }

    [When(@"the mechanic finishes the service execution with no pending parts")]
    public async Task WhenTheMechanicFinishesTheServiceExecutionWithNoPendingParts()
    {
        _lastActionResult = await _controller.FinishExecution(_serviceOrderId, hasPendingParts: false);
        var okResult = Assert.IsType<OkObjectResult>(_lastActionResult);
        _lastResponse = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
    }

    [When(@"the mechanic attempts to finish execution reporting pending parts")]
    public async Task WhenTheMechanicAttemptsToFinishExecutionReportingPendingParts()
    {
        _lastActionResult = await _controller.FinishExecution(_serviceOrderId, hasPendingParts: true);
    }

    [When(@"the vehicle is delivered to the customer")]
    public async Task WhenTheVehicleIsDeliveredToTheCustomer()
    {
        _lastActionResult = await _controller.Deliver(_serviceOrderId);
        var okResult = Assert.IsType<OkObjectResult>(_lastActionResult);
        _lastResponse = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
    }

    [When(@"an attempt is made to deliver the vehicle before completing the service")]
    public async Task WhenAnAttemptIsMadeToDeliverTheVehicleBeforeCompletingTheService()
    {
        _lastActionResult = await _controller.Deliver(_serviceOrderId);
    }

    [When(@"a part with price (.*) and quantity (\d+) is added to the Service Order")]
    public async Task WhenAPartWithPriceAndQuantityIsAddedToTheServiceOrder(string priceValue, int quantity)
    {
        var price = decimal.Parse(priceValue, CultureInfo.InvariantCulture);
        var request = new ServiceOrderAddPartRequest
        {
            PartId = Guid.NewGuid(),
            PartName = "Sample Part",
            Quantity = quantity,
            UnitPrice = price
        };

        _lastActionResult = await _controller.AddPart(_serviceOrderId, request);
        var okResult = Assert.IsType<OkObjectResult>(_lastActionResult);
        _lastResponse = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
    }

    [When(@"a service with price (.*) is added to the Service Order")]
    public async Task WhenAServiceWithPriceIsAddedToTheServiceOrder(string priceValue)
    {
        var price = decimal.Parse(priceValue, CultureInfo.InvariantCulture);
        var request = new ServiceOrderAddServiceRequest
        {
            ServiceId = Guid.NewGuid(),
            ServiceName = "Sample Service",
            Price = price
        };

        _lastActionResult = await _controller.AddService(_serviceOrderId, request);
        var okResult = Assert.IsType<OkObjectResult>(_lastActionResult);
        _lastResponse = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
    }

    [When(@"the mechanic creates the budget based on added parts and services")]
    public async Task WhenTheMechanicCreatesTheBudgetBasedOnAddedPartsAndServices()
    {
        _lastActionResult = await _controller.CreateBudget(_serviceOrderId);
        var okResult = Assert.IsType<OkObjectResult>(_lastActionResult);
        _lastResponse = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
    }

    [When(@"the mechanic finishes the technical analysis with calculated budget")]
    public async Task WhenTheMechanicFinishesTheTechnicalAnalysisWithCalculatedBudget()
    {
        _lastActionResult = await _controller.FinishAnalysis(_serviceOrderId, new ServiceOrderFinishAnalysisRequest());
        var okResult = Assert.IsType<OkObjectResult>(_lastActionResult);
        _lastResponse = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
    }

    [When(@"payment of (.*) is received for the Service Order")]
    public async Task WhenPaymentIsReceivedForTheServiceOrder(string amountValue)
    {
        var amount = decimal.Parse(amountValue, CultureInfo.InvariantCulture);
        var request = new ServiceOrderPaymentRequest
        {
            Amount = amount,
            TransactionId = "BDD-TX-100"
        };

        _lastActionResult = await _controller.ReceivePayment(_serviceOrderId, request);
        var okResult = Assert.IsType<OkObjectResult>(_lastActionResult);
        _lastResponse = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
    }

    [When(@"the Saga receives a ""PaymentCreated"" event with URL ""(.*)"" and transaction ""(.*)""")]
    public async Task WhenTheSagaReceivesAPaymentCreatedEvent(string paymentUrl, string transactionId)
    {
        var envelope = SagaEventJson.CreateEnvelope(
            SagaEventTypes.PaymentCreated,
            _serviceOrderId,
            new PaymentCreatedPayload(_serviceOrderId, Guid.NewGuid(), paymentUrl, transactionId));

        await _sagaEventProcessor.ProcessAsync(envelope);
    }

    [When(@"the Saga receives a ""PaymentConfirmed"" event with transaction ""(.*)""")]
    public async Task WhenTheSagaReceivesAPaymentConfirmedEventWithTransaction(string transactionId)
    {
        var envelope = SagaEventJson.CreateEnvelope(
            SagaEventTypes.PaymentConfirmed,
            _serviceOrderId,
            new PaymentConfirmedPayload(_serviceOrderId, transactionId));

        await _sagaEventProcessor.ProcessAsync(envelope);
    }

    [When(@"the Saga receives a ""PartStockEnsured"" event for all parts in the Service Order")]
    public async Task WhenTheSagaReceivesAPartStockEnsuredEventForAllPartsInTheServiceOrder()
    {
        var entity = await _repository.GetByIdAsync(_serviceOrderId);
        Assert.NotNull(entity);

        foreach (var part in entity.Parts.ToList())
        {
            var envelope = SagaEventJson.CreateEnvelope(
                SagaEventTypes.PartStockEnsured,
                _serviceOrderId,
                new PartStockEnsuredPayload(_serviceOrderId, part.PartId, part.Quantity));

            await _sagaEventProcessor.ProcessAsync(envelope);
        }
    }

    [When(@"the Saga receives a ""PaymentCreationFailed"" event with reason ""(.*)""")]
    public async Task WhenTheSagaReceivesAPaymentCreationFailedEventWithReason(string reason)
    {
        var envelope = SagaEventJson.CreateEnvelope(
            SagaEventTypes.PaymentCreationFailed,
            _serviceOrderId,
            new PaymentCreationFailedPayload(_serviceOrderId, reason));

        await _sagaEventProcessor.ProcessAsync(envelope);
    }

    [When(@"the Saga receives a ""PaymentRefunded"" event with transaction ""(.*)"" and reason ""(.*)""")]
    public async Task WhenTheSagaReceivesAPaymentRefundedEvent(string transactionId, string reason)
    {
        var envelope = SagaEventJson.CreateEnvelope(
            SagaEventTypes.PaymentRefunded,
            _serviceOrderId,
            new PaymentRefundedPayload(_serviceOrderId, transactionId, reason));

        await _sagaEventProcessor.ProcessAsync(envelope);
    }

    [Then(@"the Service Order should be marked as paid")]
    public async Task ThenTheServiceOrderShouldBeMarkedAsPaid()
    {
        var getByIdResult = await _controller.GetById(_serviceOrderId);
        var okResult = Assert.IsType<OkObjectResult>(getByIdResult);
        var persistedOrder = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);

        Assert.True(persistedOrder.IsPaid);
        Assert.NotNull(persistedOrder.PaidAt);
    }

    [Then(@"the Service Order should not be marked as paid")]
    public async Task ThenTheServiceOrderShouldNotBeMarkedAsPaid()
    {
        var getByIdResult = await _controller.GetById(_serviceOrderId);
        var okResult = Assert.IsType<OkObjectResult>(getByIdResult);
        var persistedOrder = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);

        Assert.False(persistedOrder.IsPaid);
        Assert.Null(persistedOrder.PaidAt);
    }

    [Then(@"the Saga event ""(.*)"" should have been published")]
    public async Task ThenTheSagaEventShouldHaveBeenPublished(string expectedEventType)
    {
        await _outboxPublisher.PublishPendingAsync(CancellationToken.None);
        Assert.Contains(expectedEventType, _publishedSagaEvents);
    }

    [Then(@"the Service Order should be created with status ""(.*)""")]
    public async Task ThenTheServiceOrderShouldBeCreatedWithStatus(string expectedStatusName)
    {
        var expectedStatus = Enum.Parse<OrderStatus>(expectedStatusName);
        Assert.NotNull(_lastResponse);
        Assert.NotEqual(Guid.Empty, _serviceOrderId);
        Assert.Equal(_customerId, _lastResponse.CustomerId);
        Assert.Equal(_vehicleId, _lastResponse.VehicleId);
        Assert.Equal(expectedStatus, _lastResponse.Status);

        var getByIdResult = await _controller.GetById(_serviceOrderId);
        var okResult = Assert.IsType<OkObjectResult>(getByIdResult);
        var persistedOrder = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
        Assert.Equal(expectedStatus, persistedOrder.Status);
    }

    [Then(@"the Service Order status should be updated to ""(.*)""")]
    [Then(@"the Service Order status should remain ""(.*)""")]
    public async Task ThenTheServiceOrderStatusShouldBe(string expectedStatusName)
    {
        var expectedStatus = Enum.Parse<OrderStatus>(expectedStatusName);

        var getByIdResult = await _controller.GetById(_serviceOrderId);
        var okResult = Assert.IsType<OkObjectResult>(getByIdResult);
        var persistedOrder = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);

        Assert.Equal(expectedStatus, persistedOrder.Status);
    }

    [Then(@"the budget recorded on the Service Order should be (.*)")]
    public async Task ThenTheBudgetRecordedOnTheServiceOrderShouldBe(string expectedBudgetValue)
    {
        var expectedBudget = decimal.Parse(expectedBudgetValue, CultureInfo.InvariantCulture);

        var getByIdResult = await _controller.GetById(_serviceOrderId);
        var okResult = Assert.IsType<OkObjectResult>(getByIdResult);
        var persistedOrder = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);

        Assert.Equal(expectedBudget, persistedOrder.Budget);
    }

    [Then(@"a status change notification for ""(.*)"" should have been published")]
    public void ThenAStatusChangeNotificationForShouldHaveBeenPublished(string expectedStatus)
    {
        _notificationPublisherMock.Verify(
            x => x.PublishNotificationAsync(
                _customerId,
                _serviceOrderId,
                expectedStatus,
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Then(@"the Service Order status history in the database should contain (\d+) status records")]
    public async Task ThenTheServiceOrderStatusHistoryInTheDatabaseShouldContainStatusRecords(int expectedCount)
    {
        var entity = await _repository.GetByIdAsync(_serviceOrderId);
        Assert.NotNull(entity);
        Assert.Equal(expectedCount, entity.StatusHistory.Count);
    }

    [Then(@"the status mean time metrics should have been recorded (\d+) times")]
    public void ThenTheStatusMeanTimeMetricsShouldHaveBeenRecordedTimes(int expectedTimes)
    {
        _metricsMock.Verify(
            x => x.CalculateServiceOrderStatusMeanTimeMetric(
                It.Is<ServiceOrder>(o => o.Id == _serviceOrderId)),
            Times.Exactly(expectedTimes));
    }

    [Then(@"the operation should be rejected with HTTP status (\d+) and message ""(.*)""")]
    public void ThenTheOperationShouldBeRejectedWithHttpStatusAndMessage(int expectedStatusCode, string expectedMessage)
    {
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(_lastActionResult);
        Assert.Equal(expectedStatusCode, badRequestResult.StatusCode ?? 400);
        Assert.NotNull(badRequestResult.Value);

        var messageProperty = badRequestResult.Value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var actualMessage = messageProperty.GetValue(badRequestResult.Value)?.ToString();
        Assert.Equal(expectedMessage, actualMessage);
    }

    public void Dispose()
    {
        _outboxServiceProvider.Dispose();
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
