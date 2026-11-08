using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OSService.Api.Controllers;
using OSService.Application.Commands;
using OSService.Application.Services;
using OSService.Domain.Entities;
using OSService.Domain.Enum;
using OSService.Domain.Interface;
using OSService.Infrastructure.Contexts;
using OSService.Infrastructure.Repositories;
using Reqnroll;

namespace OSService.Tests.BDD.Steps;

[Binding]
public sealed class ServiceOrderLifecycleSteps : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceOrderDbContext _dbContext;
    private readonly ServiceOrderRepository _repository;
    private readonly Mock<IServiceOrderNotificationPublisher> _notificationPublisherMock;
    private readonly Mock<IServiceOrderMetrics> _metricsMock;
    private readonly ServiceOrderService _serviceOrderService;
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
        _notificationPublisherMock = new Mock<IServiceOrderNotificationPublisher>();
        _metricsMock = new Mock<IServiceOrderMetrics>();

        _serviceOrderService = new ServiceOrderService(
            _repository,
            _notificationPublisherMock.Object,
            NullLogger<ServiceOrderService>.Instance,
            _metricsMock.Object);

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
            Times.Once);
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
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
