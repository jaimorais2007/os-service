using OSService.Domain.Entities;

namespace OSService.Domain.Interfaces
{
    public interface IServiceOrderRepository
    {
        Task AddAsync(ServiceOrder serviceOrder);

        Task<ServiceOrder?> GetByIdAsync(Guid id);

        Task<List<ServiceOrder>> GetAllAsync();

        Task SaveChangesAsync();
    }
}
