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

        public bool IsPaid { get; set; }

        public DateTime? PaidAt { get; set; }

        public decimal? PaidAmount { get; set; }

        public string? PaymentTransactionId { get; set; }

        public string? PaymentUrl { get; set; }

        public List<ServiceOrderPartResponse> Parts { get; set; } = new();

        public List<ServiceOrderServiceResponse> Services { get; set; } = new();
    }

    public class ServiceOrderPartResponse
    {
        public Guid Id { get; set; }

        public Guid PartId { get; set; }

        public string? PartName { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal TotalPrice { get; set; }

        public bool StockQuantityWasEnsured { get; set; }
    }

    public class ServiceOrderServiceResponse
    {
        public Guid Id { get; set; }

        public Guid ServiceId { get; set; }

        public string? ServiceName { get; set; }

        public decimal Price { get; set; }
    }
}
