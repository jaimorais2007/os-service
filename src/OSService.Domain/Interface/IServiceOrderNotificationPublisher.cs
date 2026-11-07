namespace OSService.Domain.Interface;

public interface IServiceOrderNotificationPublisher
{
    Task PublishNotificationAsync(Guid customerId, Guid serviceOrderId, string status, CancellationToken cancellationToken = default);
}
