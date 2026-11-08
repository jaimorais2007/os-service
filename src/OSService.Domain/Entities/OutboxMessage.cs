using OSService.Domain.Exceptions;

namespace OSService.Domain.Entities
{
    public class OutboxMessage
    {
        public const int MaxErrorLength = 2000;

        public Guid Id { get; private set; }

        public string EventType { get; private set; } = string.Empty;

        public Guid CorrelationId { get; private set; }

        public string Content { get; private set; } = string.Empty;

        public string? TraceParent { get; private set; }

        public DateTime OccurredAt { get; private set; }

        public int Attempts { get; private set; }

        public string? LastError { get; private set; }

        protected OutboxMessage()
        {
        }

        public OutboxMessage(
            Guid id,
            string eventType,
            Guid correlationId,
            string content,
            string? traceParent = null,
            DateTime? occurredAt = null)
        {
            if (id == Guid.Empty)
                throw new DomainException("Outbox message Id must be provided.");

            if (string.IsNullOrWhiteSpace(eventType))
                throw new DomainException("Outbox message EventType must be provided.");

            if (string.IsNullOrWhiteSpace(content))
                throw new DomainException("Outbox message Content must be provided.");

            Id = id;
            EventType = eventType;
            CorrelationId = correlationId;
            Content = content;
            TraceParent = traceParent;
            OccurredAt = occurredAt ?? DateTime.UtcNow;
            Attempts = 0;
        }

        public void RecordFailure(string error)
        {
            Attempts++;
            var safeError = error ?? string.Empty;
            LastError = safeError.Length > MaxErrorLength
                ? safeError[..MaxErrorLength]
                : safeError;
        }
    }
}
