namespace OSService.Application.Commands
{
    public class ServiceOrderRequest
    {
        public Guid CustomerId { get; set; }
        public Guid VehicleId { get; set; }
    }
}