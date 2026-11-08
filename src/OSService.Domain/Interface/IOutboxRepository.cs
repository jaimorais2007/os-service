using OSService.Domain.Entities;

namespace OSService.Domain.Interface
{
    public interface IOutboxRepository
    {
        Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
            int batchSize,
            CancellationToken cancellationToken = default);

        Task MarkPublishedAsync(
            Guid messageId,
            CancellationToken cancellationToken = default);

        Task MarkFailedAsync(
            Guid messageId,
            string error,
            CancellationToken cancellationToken = default);
    }
}
