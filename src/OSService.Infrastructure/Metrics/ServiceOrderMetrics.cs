using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OSService.Domain.Entities;
using OSService.Domain.Interface;

namespace OSService.Infrastructure.Metrics;

public class ServiceOrderMetrics : IServiceOrderMetrics
{
    public const string MeterName = "ServiceOrder.StatusMeanTime";
    public const string MetricName = "service_order_status_mean_time_in_hours";

    public static readonly Meter ServiceOrderStatusMeanTime = new(MeterName, "1.0.0");

    public static readonly Histogram<double> ServiceOrderStatusMeanTimeHistogram = ServiceOrderStatusMeanTime.CreateHistogram<double>(
        name: MetricName,
        unit: "h",
        description: "Tempo médio em que uma ordem de serviço permaneceu em um estado");

    private readonly ILogger<ServiceOrderMetrics>? _logger;

    public ServiceOrderMetrics(ILogger<ServiceOrderMetrics>? logger = null)
    {
        _logger = logger;
    }

    public void CalculateServiceOrderStatusMeanTimeMetric(ServiceOrder serviceOrder)
    {
        try
        {
            var statusList = serviceOrder.StatusHistory;

            if (statusList == null || statusList.Count <= 1)
            {
                _logger?.LogWarning("Metric {MetricName} could not be collected. Insufficient Status in service order", MeterName);
                return;
            }

            var orderedStatus = statusList
                .OrderBy(p => p.CreatedAt)
                .ThenBy(p => p.Id)
                .ToList();

            var lastStatus = orderedStatus[^2];
            var actualStatus = orderedStatus[^1];

            var durationInHours = (actualStatus.CreatedAt - lastStatus.CreatedAt).TotalHours;

            ServiceOrderStatusMeanTimeHistogram.Record(
                durationInHours,
                new KeyValuePair<string, object?>("status", lastStatus.Status.ToString()),
                new KeyValuePair<string, object?>("entity_type", nameof(ServiceOrder))
            );
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Exception at metric {MetricName} collect.", MeterName);
        }
    }
}
