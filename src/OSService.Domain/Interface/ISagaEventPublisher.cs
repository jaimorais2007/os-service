namespace OSService.Domain.Interface
{
    public interface ISagaEventPublisher
    {
        Task PublishAsync<TPayload>(
            string eventType,
            Guid correlationId,
            TPayload payload,
            CancellationToken cancellationToken = default);
    }
}
