namespace OSService.Domain.Interface
{
    public interface ISagaEventPublisher
    {
        Task PublishAsync<TPayload>(
            string eventType,
            Guid correlationId,
            TPayload payload,
            CancellationToken cancellationToken = default);

        Task PublishRawAsync(
            string eventType,
            string content,
            string? traceParent = null,
            CancellationToken cancellationToken = default);
    }
}
