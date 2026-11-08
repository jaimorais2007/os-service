using Microsoft.EntityFrameworkCore;
using OSService.Domain.Entities;
using OSService.Domain.Enum;
using OSService.Domain.Interfaces;
using OSService.Infrastructure.Contexts;

namespace OSService.Infrastructure.Repositories
{
    public class ServiceOrderRepository : IServiceOrderRepository
    {
        private readonly ServiceOrderDbContext _context;

        public ServiceOrderRepository(ServiceOrderDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(ServiceOrder serviceOrder)
        {
            await _context.ServiceOrders.AddAsync(serviceOrder);
        }

        public async Task<ServiceOrder?> GetByIdAsync(Guid id)
        {
            return await _context.ServiceOrders
                .Include(x => x.StatusHistory)
                .Include(x => x.Parts)
                .Include(x => x.Services)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<List<ServiceOrder>> GetAllAsync()
        {
            return await GetAllAsync(null);
        }

        public async Task<List<ServiceOrder>> GetAllAsync(OrderStatus? status)
        {
            var orders = await _context.ServiceOrders
                .AsNoTracking()
                .Include(x => x.StatusHistory)
                .Include(x => x.Parts)
                .Include(x => x.Services)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            if (status.HasValue)
            {
                return orders
                    .Where(x => x.GetCurrentStatus() == status.Value)
                    .ToList();
            }

            return orders;
        }

        public async Task<List<ServiceOrder>> GetByStatusAsync(OrderStatus status)
        {
            return await GetAllAsync(status);
        }

        public async Task<bool> IsEventProcessedAsync(
            Guid eventId,
            CancellationToken cancellationToken = default)
        {
            return await _context.ProcessedSagaEvents
                .AsNoTracking()
                .AnyAsync(x => x.EventId == eventId, cancellationToken);
        }

        public async Task AddProcessedEventAsync(
            ProcessedSagaEvent processedEvent,
            CancellationToken cancellationToken = default)
        {
            await _context.ProcessedSagaEvents.AddAsync(processedEvent, cancellationToken);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
