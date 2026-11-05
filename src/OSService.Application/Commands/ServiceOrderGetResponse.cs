using OSService.Domain.Enum;

namespace OSService.Application.Commands
{
    public class ServiceOrderGetResponse
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Guid VehicleId { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal Budget { get; set; }
        public bool Inactive { get; set; }
        public OrderStatus Status { get; set; }
    }
}