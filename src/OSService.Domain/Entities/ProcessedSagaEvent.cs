using OSService.Domain.Exceptions;

namespace OSService.Domain.Entities
{
    public class ProcessedSagaEvent
    {
        public Guid EventId { get; private set; }

        public string EventType { get; private set; } = string.Empty;

        public Guid CorrelationId { get; private set; }

        public DateTime ProcessedAt { get; private set; }

        protected ProcessedSagaEvent()
        {
        }

        public ProcessedSagaEvent(Guid eventId, string eventType, Guid correlationId)
        {
            if (eventId == Guid.Empty)
                throw new DomainException("EventId must be provided.");

            if (string.IsNullOrWhiteSpace(eventType))
                throw new DomainException("EventType must be provided.");

            EventId = eventId;
            EventType = eventType;
            CorrelationId = correlationId;
            ProcessedAt = DateTime.UtcNow;
        }
    }
}
