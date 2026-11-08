using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using OSService.Domain.Entities;

namespace OSService.Application.Saga
{
    public sealed record EventEnvelope(
        Guid EventId,
        string EventType,
        DateTime OccurredAt,
        Guid CorrelationId,
        JsonElement Payload);

    public static class SagaEventTypes
    {
        public const string BudgetApproved = "BudgetApproved";
        public const string PaymentCreated = "PaymentCreated";
        public const string PaymentCreationFailed = "PaymentCreationFailed";
        public const string PaymentConfirmed = "PaymentConfirmed";
        public const string PaymentExpired = "PaymentExpired";
        public const string ServiceOrderApproved = "ServiceOrderApproved";
        public const string PartStockEnsured = "PartStockEnsured";
        public const string PaymentRefunded = "PaymentRefunded";
        public const string ServiceOrderRejected = "ServiceOrderRejected";
    }

    public static class SagaEventJson
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public static EventEnvelope CreateEnvelope<TPayload>(
            string eventType,
            Guid correlationId,
            TPayload payload,
            Guid? eventId = null)
        {
            return new EventEnvelope(
                eventId ?? Guid.NewGuid(),
                eventType,
                DateTime.UtcNow,
                correlationId,
                JsonSerializer.SerializeToElement(payload, Options));
        }

        public static OutboxMessage CreateOutboxMessage<TPayload>(
            string eventType,
            Guid correlationId,
            TPayload payload)
        {
            var id = Guid.NewGuid();
            var occurredAt = DateTime.UtcNow;
            var envelope = new EventEnvelope(
                id,
                eventType,
                occurredAt,
                correlationId,
                JsonSerializer.SerializeToElement(payload, Options));

            var content = JsonSerializer.Serialize(envelope, Options);
            return new OutboxMessage(
                id,
                eventType,
                correlationId,
                content,
                Activity.Current?.Id,
                occurredAt);
        }
    }

    public sealed record BudgetApprovedPart(Guid PartId, int Quantity);

    public sealed record BudgetApprovedPayload(
        Guid ServiceOrderId,
        decimal TotalAmount,
        IReadOnlyList<Guid>? ServiceIds,
        IReadOnlyList<BudgetApprovedPart>? Parts);

    public sealed record PaymentCreatedPayload(
        Guid ServiceOrderId,
        Guid PaymentId,
        string PaymentUrl,
        string GatewayTransactionId);

    public sealed record PaymentCreationFailedPayload(
        Guid ServiceOrderId,
        string Reason);

    public sealed record PaymentConfirmedPayload(
        Guid ServiceOrderId,
        string GatewayTransactionId);

    public sealed record PaymentExpiredPayload(
        Guid ServiceOrderId,
        string GatewayTransactionId);

    public sealed record ServiceOrderApprovedPart(Guid PartId, int Quantity);

    public sealed record ServiceOrderApprovedPayload(
        Guid ServiceOrderId,
        IReadOnlyList<ServiceOrderApprovedPart>? Parts);

    public sealed record PartStockEnsuredPayload(
        Guid ServiceOrderId,
        Guid PartId,
        int Quantity);

    public sealed record PaymentRefundedPayload(
        Guid ServiceOrderId,
        string GatewayTransactionId,
        string Reason);

    public sealed record ServiceOrderRejectedPayload(Guid ServiceOrderId);
}
