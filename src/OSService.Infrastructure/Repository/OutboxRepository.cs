using Microsoft.EntityFrameworkCore;
using OSService.Domain.Entities;
using OSService.Domain.Interface;
using OSService.Infrastructure.Contexts;

namespace OSService.Infrastructure.Repositories
{
    public class OutboxRepository : IOutboxRepository
    {
        private readonly ServiceOrderDbContext _context;

        public OutboxRepository(ServiceOrderDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            return await _context.OutboxMessages
                .AsNoTracking()
                .OrderBy(x => x.OccurredAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
        }

        public async Task MarkPublishedAsync(
            Guid messageId,
            CancellationToken cancellationToken = default)
        {
            var message = await _context.OutboxMessages
                .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);

            if (message == null)
                return;

            _context.OutboxMessages.Remove(message);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkFailedAsync(
            Guid messageId,
            string error,
            CancellationToken cancellationToken = default)
        {
            var message = await _context.OutboxMessages
                .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);

            if (message == null)
                return;

            message.RecordFailure(error);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
