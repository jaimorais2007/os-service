namespace OSService.Domain.Enum
{
    public enum OrderStatus
    {
        Received = 1,
        UnderAnalysis = 2,
        AwaitingApproval = 3,
        InProgress = 4,
        Completed = 5,
        Delivered = 6,
        Rejected = 7,
        WaitingPayment = 8
    }
}
