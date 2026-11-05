
using Microsoft.EntityFrameworkCore;
using OSService.Domain.Entities;
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
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<List<ServiceOrder>> GetAllAsync()
        {
            return await _context.ServiceOrders
                .AsNoTracking()
                .Include(x => x.StatusHistory)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
