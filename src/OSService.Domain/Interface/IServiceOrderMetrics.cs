using OSService.Domain.Entities;

namespace OSService.Domain.Interface;

public interface IServiceOrderMetrics
{
    void CalculateServiceOrderStatusMeanTimeMetric(ServiceOrder serviceOrder);
}
