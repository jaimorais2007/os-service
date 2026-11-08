namespace OSService.Application.Commands
{
    public class ServiceOrderAddServiceRequest
    {
        public Guid ServiceId { get; set; }

        public string? ServiceName { get; set; }

        public decimal Price { get; set; }
    }
}
