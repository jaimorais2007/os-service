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

        Task<bool> IsEventProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);

        Task AddProcessedEventAsync(ProcessedSagaEvent processedEvent, CancellationToken cancellationToken = default);

        Task AddOutboxMessageAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken = default);

        Task SaveChangesAsync();
    }
}
