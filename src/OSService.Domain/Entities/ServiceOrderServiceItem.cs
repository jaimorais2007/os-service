using OSService.Domain.Exceptions;

namespace OSService.Domain.Entities
{
    public class ServiceOrderServiceItem
    {
        public Guid Id { get; private set; }

        public Guid ServiceOrderId { get; private set; }

        public ServiceOrder ServiceOrder { get; private set; } = null!;

        public Guid ServiceId { get; private set; }

        public string? ServiceName { get; private set; }

        public decimal Price { get; private set; }

        protected ServiceOrderServiceItem()
        {
        }

        public ServiceOrderServiceItem(
            Guid serviceOrderId,
            Guid serviceId,
            decimal price = 0,
            string? serviceName = null)
        {
            if (serviceId == Guid.Empty)
                throw new DomainException("Service must be provided.");

            if (price < 0)
                throw new DomainException("Price cannot be negative.");

            Id = Guid.NewGuid();
            ServiceOrderId = serviceOrderId;
            ServiceId = serviceId;
            Price = price;
            ServiceName = serviceName;
        }

        public void UpdatePrice(decimal price)
        {
            if (price < 0)
                throw new DomainException("Price cannot be negative.");

            Price = price;
        }
    }
}
