namespace OSService.Application.Commands
{
    public class ServiceOrderAddPartRequest
    {
        public Guid PartId { get; set; }

        public string? PartName { get; set; }

        public int Quantity { get; set; } = 1;

        public decimal UnitPrice { get; set; }

        public bool StockQuantityWasEnsured { get; set; } = false;
    }
}
