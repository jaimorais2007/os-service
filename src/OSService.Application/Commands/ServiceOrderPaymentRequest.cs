namespace OSService.Application.Commands
{
    public class ServiceOrderPaymentRequest
    {
        public decimal? Amount { get; set; }

        public string? TransactionId { get; set; }
    }
}
