using OSService.Domain.Exceptions;

namespace OSService.Domain.Entities
{
    public class ServiceOrderPart
    {
        public Guid Id { get; private set; }

        public Guid ServiceOrderId { get; private set; }

        public ServiceOrder ServiceOrder { get; private set; } = null!;

        public Guid PartId { get; private set; }

        public string? PartName { get; private set; }

        public int Quantity { get; private set; }

        public decimal UnitPrice { get; private set; }

        public decimal TotalPrice => Quantity * UnitPrice;

        public bool StockQuantityWasEnsured { get; private set; }

        protected ServiceOrderPart()
        {
        }

        public ServiceOrderPart(
            Guid serviceOrderId,
            Guid partId,
            int quantity,
            decimal unitPrice = 0,
            string? partName = null,
            bool stockQuantityWasEnsured = false)
        {
            if (partId == Guid.Empty)
                throw new DomainException("Part must be provided.");

            if (quantity <= 0)
                throw new DomainException("Quantity must be greater than zero.");

            if (unitPrice < 0)
                throw new DomainException("Unit price cannot be negative.");

            Id = Guid.NewGuid();
            ServiceOrderId = serviceOrderId;
            PartId = partId;
            Quantity = quantity;
            UnitPrice = unitPrice;
            PartName = partName;
            StockQuantityWasEnsured = stockQuantityWasEnsured;
        }

        public void UpdateQuantity(int quantity)
        {
            if (quantity <= 0)
                throw new DomainException("Quantity must be greater than zero.");

            Quantity = quantity;
        }

        public void UpdateUnitPrice(decimal unitPrice)
        {
            if (unitPrice < 0)
                throw new DomainException("Unit price cannot be negative.");

            UnitPrice = unitPrice;
        }

        public void EnsureStock()
        {
            StockQuantityWasEnsured = true;
        }

        public void ResetStockEnsured()
        {
            StockQuantityWasEnsured = false;
        }
    }
}
