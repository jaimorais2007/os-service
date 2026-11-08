using System.Text.Json;
using Microsoft.Extensions.Logging;
using OSService.Domain.Entities;
using OSService.Domain.Enum;
using OSService.Domain.Interface;
using OSService.Domain.Interfaces;

namespace OSService.Application.Saga
{
    public class SagaEventProcessor
    {
        private readonly IServiceOrderRepository _repository;
        private readonly ISagaEventPublisher? _sagaEventPublisher;
        private readonly IServiceOrderNotificationPublisher? _notificationPublisher;
        private readonly IServiceOrderMetrics? _metrics;
        private readonly ILogger<SagaEventProcessor>? _logger;

        public SagaEventProcessor(
            IServiceOrderRepository repository,
            ISagaEventPublisher? sagaEventPublisher = null,
            IServiceOrderNotificationPublisher? notificationPublisher = null,
            IServiceOrderMetrics? metrics = null,
            ILogger<SagaEventProcessor>? logger = null)
        {
            _repository = repository;
            _sagaEventPublisher = sagaEventPublisher;
            _notificationPublisher = notificationPublisher;
            _metrics = metrics;
            _logger = logger;
        }

        public async Task ProcessAsync(
            EventEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(envelope);

            if (envelope.EventId == Guid.Empty || string.IsNullOrWhiteSpace(envelope.EventType))
            {
                _logger?.LogWarning("Skipping Saga event with empty EventId or EventType.");
                return;
            }

            if (await _repository.IsEventProcessedAsync(envelope.EventId, cancellationToken))
            {
                _logger?.LogInformation(
                    "Saga event {EventId} ({EventType}) was already processed. Skipping.",
                    envelope.EventId,
                    envelope.EventType);
                return;
            }

            switch (envelope.EventType)
            {
                case SagaEventTypes.PaymentCreated:
                    await HandlePaymentCreatedAsync(envelope, cancellationToken);
                    break;

                case SagaEventTypes.PaymentCreationFailed:
                    await HandlePaymentCreationFailedAsync(envelope, cancellationToken);
                    break;

                case SagaEventTypes.PaymentExpired:
                    await HandlePaymentExpiredAsync(envelope, cancellationToken);
                    break;

                case SagaEventTypes.PaymentConfirmed:
                    await HandlePaymentConfirmedAsync(envelope, cancellationToken);
                    break;

                case SagaEventTypes.PartStockEnsured:
                    await HandlePartStockEnsuredAsync(envelope, cancellationToken);
                    break;

                case SagaEventTypes.PaymentRefunded:
                    await HandlePaymentRefundedAsync(envelope, cancellationToken);
                    break;

                default:
                    _logger?.LogInformation(
                        "Unhandled Saga event type {EventType} (EventId: {EventId}).",
                        envelope.EventType,
                        envelope.EventId);
                    break;
            }
        }

        private async Task HandlePaymentCreatedAsync(
            EventEnvelope envelope,
            CancellationToken cancellationToken)
        {
            var payload = envelope.Payload.Deserialize<PaymentCreatedPayload>(SagaEventJson.Options);
            if (payload == null)
                return;

            var serviceOrder = await _repository.GetByIdAsync(payload.ServiceOrderId);
            if (serviceOrder == null)
            {
                _logger?.LogWarning(
                    "ServiceOrder {ServiceOrderId} not found for PaymentCreated event {EventId}.",
                    payload.ServiceOrderId,
                    envelope.EventId);
                return;
            }

            serviceOrder.RegisterPaymentCreated(payload.PaymentUrl, payload.GatewayTransactionId);

            await MarkProcessedAndSaveAsync(envelope, cancellationToken);

            _logger?.LogInformation(
                "PaymentCreated registered on ServiceOrder {ServiceOrderId}. TransactionId: {TransactionId}",
                serviceOrder.Id,
                payload.GatewayTransactionId);
        }

        private async Task HandlePaymentCreationFailedAsync(
            EventEnvelope envelope,
            CancellationToken cancellationToken)
        {
            var payload = envelope.Payload.Deserialize<PaymentCreationFailedPayload>(SagaEventJson.Options);
            if (payload == null)
                return;

            var serviceOrder = await _repository.GetByIdAsync(payload.ServiceOrderId);
            if (serviceOrder == null)
            {
                _logger?.LogWarning(
                    "ServiceOrder {ServiceOrderId} not found for PaymentCreationFailed event {EventId}.",
                    payload.ServiceOrderId,
                    envelope.EventId);
                return;
            }

            var previousStatus = serviceOrder.GetCurrentStatus();
            serviceOrder.RevertToAwaitingApproval();

            await MarkProcessedAndSaveAsync(envelope, cancellationToken);

            _logger?.LogWarning(
                "Compensation 1A executed for ServiceOrder {ServiceOrderId} due to payment creation failure: {Reason}",
                serviceOrder.Id,
                payload.Reason);

            await NotifyIfStatusChangedAsync(serviceOrder, previousStatus, cancellationToken);
        }

        private async Task HandlePaymentExpiredAsync(
            EventEnvelope envelope,
            CancellationToken cancellationToken)
        {
            var payload = envelope.Payload.Deserialize<PaymentExpiredPayload>(SagaEventJson.Options);
            if (payload == null)
                return;

            var serviceOrder = await _repository.GetByIdAsync(payload.ServiceOrderId);
            if (serviceOrder == null)
            {
                _logger?.LogWarning(
                    "ServiceOrder {ServiceOrderId} not found for PaymentExpired event {EventId}.",
                    payload.ServiceOrderId,
                    envelope.EventId);
                return;
            }

            var previousStatus = serviceOrder.GetCurrentStatus();
            serviceOrder.RevertToAwaitingApproval();

            await MarkProcessedAndSaveAsync(envelope, cancellationToken);

            _logger?.LogWarning(
                "Compensation 1B executed for ServiceOrder {ServiceOrderId} due to expired/refused payment {TransactionId}.",
                serviceOrder.Id,
                payload.GatewayTransactionId);

            await NotifyIfStatusChangedAsync(serviceOrder, previousStatus, cancellationToken);
        }

        private async Task HandlePaymentConfirmedAsync(
            EventEnvelope envelope,
            CancellationToken cancellationToken)
        {
            var payload = envelope.Payload.Deserialize<PaymentConfirmedPayload>(SagaEventJson.Options);
            if (payload == null)
                return;

            var serviceOrder = await _repository.GetByIdAsync(payload.ServiceOrderId);
            if (serviceOrder == null)
            {
                _logger?.LogWarning(
                    "ServiceOrder {ServiceOrderId} not found for PaymentConfirmed event {EventId}.",
                    payload.ServiceOrderId,
                    envelope.EventId);
                return;
            }

            var previousStatus = serviceOrder.GetCurrentStatus();
            var hasPendingParts = serviceOrder.Parts.Count > 0 &&
                                  serviceOrder.Parts.Any(p => !p.StockQuantityWasEnsured);

            serviceOrder.ConfirmPayment(payload.GatewayTransactionId);

            if (hasPendingParts && _sagaEventPublisher != null)
            {
                var approvedPayload = new ServiceOrderApprovedPayload(
                    serviceOrder.Id,
                    serviceOrder.Parts
                        .Select(p => new ServiceOrderApprovedPart(p.PartId, p.Quantity))
                        .ToList());

                await _sagaEventPublisher.PublishAsync(
                    SagaEventTypes.ServiceOrderApproved,
                    serviceOrder.Id,
                    approvedPayload,
                    cancellationToken);
            }

            await MarkProcessedAndSaveAsync(envelope, cancellationToken);

            _logger?.LogInformation(
                "PaymentConfirmed processed for ServiceOrder {ServiceOrderId}. Status: {Status}, HasPendingParts: {HasPendingParts}",
                serviceOrder.Id,
                serviceOrder.GetCurrentStatus(),
                hasPendingParts);

            await NotifyIfStatusChangedAsync(serviceOrder, previousStatus, cancellationToken);
        }

        private async Task HandlePartStockEnsuredAsync(
            EventEnvelope envelope,
            CancellationToken cancellationToken)
        {
            var payload = envelope.Payload.Deserialize<PartStockEnsuredPayload>(SagaEventJson.Options);
            if (payload == null)
                return;

            var serviceOrder = await _repository.GetByIdAsync(payload.ServiceOrderId);
            if (serviceOrder == null)
            {
                _logger?.LogWarning(
                    "ServiceOrder {ServiceOrderId} not found for PartStockEnsured event {EventId}.",
                    payload.ServiceOrderId,
                    envelope.EventId);
                return;
            }

            var previousStatus = serviceOrder.GetCurrentStatus();
            serviceOrder.ConfirmPartStockEnsured(payload.PartId);

            await MarkProcessedAndSaveAsync(envelope, cancellationToken);

            _logger?.LogInformation(
                "PartStockEnsured processed for ServiceOrder {ServiceOrderId}, Part {PartId}. CurrentStatus: {Status}",
                serviceOrder.Id,
                payload.PartId,
                serviceOrder.GetCurrentStatus());

            await NotifyIfStatusChangedAsync(serviceOrder, previousStatus, cancellationToken);
        }

        private async Task HandlePaymentRefundedAsync(
            EventEnvelope envelope,
            CancellationToken cancellationToken)
        {
            var payload = envelope.Payload.Deserialize<PaymentRefundedPayload>(SagaEventJson.Options);
            if (payload == null)
                return;

            var serviceOrder = await _repository.GetByIdAsync(payload.ServiceOrderId);
            if (serviceOrder == null)
            {
                _logger?.LogWarning(
                    "ServiceOrder {ServiceOrderId} not found for PaymentRefunded event {EventId}.",
                    payload.ServiceOrderId,
                    envelope.EventId);
                return;
            }

            var previousStatus = serviceOrder.GetCurrentStatus();
            serviceOrder.RevertToAwaitingApproval();

            await MarkProcessedAndSaveAsync(envelope, cancellationToken);

            _logger?.LogWarning(
                "Compensation 2 executed for ServiceOrder {ServiceOrderId}: payment {TransactionId} refunded ({Reason}). Reverted to AwaitingApproval.",
                serviceOrder.Id,
                payload.GatewayTransactionId,
                payload.Reason);

            await NotifyIfStatusChangedAsync(serviceOrder, previousStatus, cancellationToken);
        }

        private async Task MarkProcessedAndSaveAsync(
            EventEnvelope envelope,
            CancellationToken cancellationToken)
        {
            await _repository.AddProcessedEventAsync(
                new ProcessedSagaEvent(envelope.EventId, envelope.EventType, envelope.CorrelationId),
                cancellationToken);

            await _repository.SaveChangesAsync();
        }

        private async Task NotifyIfStatusChangedAsync(
            ServiceOrder serviceOrder,
            OrderStatus previousStatus,
            CancellationToken cancellationToken)
        {
            var currentStatus = serviceOrder.GetCurrentStatus();
            if (currentStatus == previousStatus)
                return;

            if (_metrics != null)
            {
                try
                {
                    _metrics.CalculateServiceOrderStatusMeanTimeMetric(serviceOrder);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to record metrics for ServiceOrder {ServiceOrderId}", serviceOrder.Id);
                }
            }

            if (_notificationPublisher != null)
            {
                try
                {
                    await _notificationPublisher.PublishNotificationAsync(
                        serviceOrder.CustomerId,
                        serviceOrder.Id,
                        currentStatus.ToString(),
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to publish notification for ServiceOrder {ServiceOrderId}", serviceOrder.Id);
                }
            }
        }
    }
}
