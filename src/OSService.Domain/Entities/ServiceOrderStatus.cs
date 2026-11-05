
using OSService.Domain.Enum;

namespace OSService.Domain.Entities
{
    public class ServiceOrderStatus
    {
        public Guid Id { get; private set; }
        public Guid ServiceOrderId { get; private set; }
        public OrderStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public ServiceOrder ServiceOrder { get; private set; } = null!;

        protected ServiceOrderStatus()
        {
        }

        public ServiceOrderStatus(Guid serviceOrderId, OrderStatus status)
        {
            Id = Guid.NewGuid();
            ServiceOrderId = serviceOrderId;
            Status = status;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
