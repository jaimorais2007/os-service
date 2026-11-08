using OSService.Domain.Entities;
using OSService.Domain.Enum;

namespace OSService.Domain.Interfaces
{
    public interface IServiceOrderRepository
    {
        Task AddAsync(ServiceOrder serviceOrder);

        Task<ServiceOrder?> GetByIdAsync(Guid id);

        Task<List<ServiceOrder>> GetAllAsync();

        Task<List<ServiceOrder>> GetAllAsync(OrderStatus? status);

        Task<List<ServiceOrder>> GetByStatusAsync(OrderStatus status);

        Task SaveChangesAsync();
    }
}
