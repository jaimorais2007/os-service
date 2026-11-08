using System.Text.Json;
using Moq;
using OSService.Application.Saga;
using OSService.Domain.Entities;
using OSService.Domain.Enum;
using OSService.Domain.Interface;
using OSService.Domain.Interfaces;
using Xunit;

namespace OSService.Tests.Saga;

public class SagaEventProcessorTests
{
    private readonly Mock<IServiceOrderRepository> _repositoryMock = new();
    private readonly Mock<IServiceOrderNotificationPublisher> _notificationMock = new();
    private readonly Mock<IServiceOrderMetrics> _metricsMock = new();
    private readonly SagaEventProcessor _processor;

    public SagaEventProcessorTests()
    {
        _processor = new SagaEventProcessor(
            _repositoryMock.Object,
            _notificationMock.Object,
            _metricsMock.Object);
    }

    private static ServiceOrder CreateWaitingPaymentOrder(bool withParts = false)
    {
        var order = new ServiceOrder(Guid.NewGuid(), Guid.NewGuid());
        if (withParts)
        {
            order.AddPart(Guid.NewGuid(), 2, 100m, "Brake Pad");
        }

        order.StartDiagnostics();
        order.FinishAnalysis(300m);
        order.Approve();
        return order;
    }

    [Fact]
    public async Task ProcessAsync_WhenAlreadyProcessed_ShouldSkipExecution()
    {
        var envelope = SagaEventJson.CreateEnvelope(
            SagaEventTypes.PaymentConfirmed,
            Guid.NewGuid(),
            new PaymentConfirmedPayload(Guid.NewGuid(), "TX-1"));

        _repositoryMock
            .Setup(x => x.IsEventProcessedAsync(envelope.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _processor.ProcessAsync(envelope);

        _repositoryMock.Verify(x => x.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
        _repositoryMock.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_PaymentCreated_ShouldUpdatePaymentUrlAndTransactionId()
    {
        var order = CreateWaitingPaymentOrder();
        _repositoryMock.Setup(x => x.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var envelope = SagaEventJson.CreateEnvelope(
            SagaEventTypes.PaymentCreated,
            order.Id,
            new PaymentCreatedPayload(order.Id, Guid.NewGuid(), "https://mp.com/pay/123", "PREF-123"));

        await _processor.ProcessAsync(envelope);

        Assert.Equal("https://mp.com/pay/123", order.PaymentUrl);
        Assert.Equal("PREF-123", order.PaymentTransactionId);
        Assert.Equal(OrderStatus.WaitingPayment, order.GetCurrentStatus());
        _repositoryMock.Verify(x => x.AddProcessedEventAsync(It.IsAny<ProcessedSagaEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_PaymentCreationFailed_ShouldRevertToAwaitingApprovalAndNotify()
    {
        var order = CreateWaitingPaymentOrder();
        _repositoryMock.Setup(x => x.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var envelope = SagaEventJson.CreateEnvelope(
            SagaEventTypes.PaymentCreationFailed,
            order.Id,
            new PaymentCreationFailedPayload(order.Id, "Gateway error"));

        await _processor.ProcessAsync(envelope);

        Assert.Equal(OrderStatus.AwaitingApproval, order.GetCurrentStatus());
        _notificationMock.Verify(
            x => x.PublishNotificationAsync(order.CustomerId, order.Id, "AwaitingApproval", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_PaymentExpired_ShouldRevertToAwaitingApprovalAndNotify()
    {
        var order = CreateWaitingPaymentOrder();
        _repositoryMock.Setup(x => x.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var envelope = SagaEventJson.CreateEnvelope(
            SagaEventTypes.PaymentExpired,
            order.Id,
            new PaymentExpiredPayload(order.Id, "PREF-EXP"));

        await _processor.ProcessAsync(envelope);

        Assert.Equal(OrderStatus.AwaitingApproval, order.GetCurrentStatus());
        _notificationMock.Verify(
            x => x.PublishNotificationAsync(order.CustomerId, order.Id, "AwaitingApproval", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_PaymentConfirmedWithoutParts_ShouldTransitionToInProgressImmediately()
    {
        var order = CreateWaitingPaymentOrder(withParts: false);
        _repositoryMock.Setup(x => x.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var envelope = SagaEventJson.CreateEnvelope(
            SagaEventTypes.PaymentConfirmed,
            order.Id,
            new PaymentConfirmedPayload(order.Id, "PREF-OK"));

        await _processor.ProcessAsync(envelope);

        Assert.True(order.IsPaid);
        Assert.Equal(OrderStatus.InProgress, order.GetCurrentStatus());
        _repositoryMock.Verify(
            x => x.AddOutboxMessageAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _notificationMock.Verify(
            x => x.PublishNotificationAsync(order.CustomerId, order.Id, "InProgress", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_PaymentConfirmedWithParts_ShouldStageServiceOrderApprovedOutboxAndWaitStock()
    {
        var order = CreateWaitingPaymentOrder(withParts: true);
        var partId = order.Parts.First().PartId;
        OutboxMessage? capturedOutbox = null;
        _repositoryMock.Setup(x => x.GetByIdAsync(order.Id)).ReturnsAsync(order);
        _repositoryMock
            .Setup(x => x.AddOutboxMessageAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, CancellationToken>((msg, _) => capturedOutbox = msg)
            .Returns(Task.CompletedTask);

        var paymentEnvelope = SagaEventJson.CreateEnvelope(
            SagaEventTypes.PaymentConfirmed,
            order.Id,
            new PaymentConfirmedPayload(order.Id, "PREF-PARTS"));

        await _processor.ProcessAsync(paymentEnvelope);

        Assert.True(order.IsPaid);
        Assert.Equal(OrderStatus.WaitingPayment, order.GetCurrentStatus());
        Assert.NotNull(capturedOutbox);
        Assert.Equal(SagaEventTypes.ServiceOrderApproved, capturedOutbox.EventType);
        Assert.Equal(order.Id, capturedOutbox.CorrelationId);

        var stagedEnvelope = JsonSerializer.Deserialize<EventEnvelope>(capturedOutbox.Content, SagaEventJson.Options);
        Assert.NotNull(stagedEnvelope);
        var stagedPayload = JsonSerializer.Deserialize<ServiceOrderApprovedPayload>(stagedEnvelope.Payload, SagaEventJson.Options);
        Assert.NotNull(stagedPayload);
        Assert.Equal(order.Id, stagedPayload.ServiceOrderId);
        Assert.NotNull(stagedPayload.Parts);
        Assert.Single(stagedPayload.Parts);
        Assert.Equal(partId, stagedPayload.Parts[0].PartId);
        Assert.Equal(2, stagedPayload.Parts[0].Quantity);

        var stockEnvelope = SagaEventJson.CreateEnvelope(
            SagaEventTypes.PartStockEnsured,
            order.Id,
            new PartStockEnsuredPayload(order.Id, partId, 2));

        await _processor.ProcessAsync(stockEnvelope);

        Assert.Equal(OrderStatus.InProgress, order.GetCurrentStatus());
        Assert.True(order.Parts.First().StockQuantityWasEnsured);
        _notificationMock.Verify(
            x => x.PublishNotificationAsync(order.CustomerId, order.Id, "InProgress", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_PaymentRefunded_ShouldClearPaidFlagAndRevertToAwaitingApproval()
    {
        var order = CreateWaitingPaymentOrder(withParts: true);
        order.ConfirmPayment("PREF-REFUND");
        _repositoryMock.Setup(x => x.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var envelope = SagaEventJson.CreateEnvelope(
            SagaEventTypes.PaymentRefunded,
            order.Id,
            new PaymentRefundedPayload(order.Id, "PREF-REFUND", "Insufficient stock for part"));

        await _processor.ProcessAsync(envelope);

        Assert.False(order.IsPaid);
        Assert.Equal(OrderStatus.AwaitingApproval, order.GetCurrentStatus());
        _notificationMock.Verify(
            x => x.PublishNotificationAsync(order.CustomerId, order.Id, "AwaitingApproval", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
