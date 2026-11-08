using Microsoft.Extensions.Logging;
using OSService.Application.Commands;
using OSService.Domain.Entities;
using OSService.Domain.Interface;
using OSService.Domain.Interfaces;

namespace OSService.Application.Services
{
    public class ServiceOrderService
    {
        private readonly IServiceOrderRepository _repository;
        private readonly IServiceOrderNotificationPublisher? _notificationPublisher;
        private readonly ILogger<ServiceOrderService>? _logger;
        private readonly IServiceOrderMetrics? _metrics;

        public ServiceOrderService(
            IServiceOrderRepository repository,
            IServiceOrderNotificationPublisher? notificationPublisher = null,
            ILogger<ServiceOrderService>? logger = null,
            IServiceOrderMetrics? metrics = null)
        {
            _repository = repository;
            _notificationPublisher = notificationPublisher;
            _logger = logger;
            _metrics = metrics;
        }

        public async Task<ServiceOrderGetResponse> CreateAsync(
            ServiceOrderRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var serviceOrder = new ServiceOrder(
                request.CustomerId,
                request.VehicleId);

            await _repository.AddAsync(serviceOrder);
            await _repository.SaveChangesAsync();

            _logger?.LogInformation(
                "Service Order successfully created. Id: {ServiceOrder}",
                serviceOrder.Id);

            await TryPublishNotificationAsync(
                serviceOrder.CustomerId,
                serviceOrder.Id,
                serviceOrder.GetCurrentStatus().ToString());

            return MapResponse(serviceOrder);
        }

        public async Task<ServiceOrderGetResponse?> GetByIdAsync(Guid id)
        {
            var serviceOrder = await _repository.GetByIdAsync(id);

            if (serviceOrder == null)
                return null;

            return MapResponse(serviceOrder);
        }

        public async Task<List<ServiceOrderGetResponse>> GetAllAsync()
        {
            var serviceOrders = await _repository.GetAllAsync();

            return serviceOrders
                .Select(MapResponse)
                .ToList();
        }

        private static ServiceOrderGetResponse MapResponse(
            ServiceOrder serviceOrder)
        {
            return new ServiceOrderGetResponse
            {
                Id = serviceOrder.Id,
                CustomerId = serviceOrder.CustomerId,
                VehicleId = serviceOrder.VehicleId,
                CreatedAt = serviceOrder.CreatedAt,
                Budget = serviceOrder.Budget,
                Inactive = serviceOrder.Inactive,
                Status = serviceOrder.GetCurrentStatus()
            };
        }

        #region Service Order Status Changes

        public async Task<ServiceOrderGetResponse> StartDiagnosticsAsync(Guid id)
        {
            var serviceOrder = await FindEntityAsync(id);

            serviceOrder.StartDiagnostics();

            await _repository.SaveChangesAsync();

            TryRecordMetrics(serviceOrder);

            await TryPublishNotificationAsync(
                serviceOrder.CustomerId,
                serviceOrder.Id,
                serviceOrder.GetCurrentStatus().ToString());

            return MapResponse(serviceOrder);
        }

        public async Task<ServiceOrderGetResponse> FinishAnalysisAsync(
            Guid id,
            ServiceOrderFinishAnalysisRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var serviceOrder = await FindEntityAsync(id);

            serviceOrder.FinishAnalysis(request.Budget);

            await _repository.SaveChangesAsync();

            TryRecordMetrics(serviceOrder);

            await TryPublishNotificationAsync(
                serviceOrder.CustomerId,
                serviceOrder.Id,
                serviceOrder.GetCurrentStatus().ToString());

            return MapResponse(serviceOrder);
        }

        public async Task<ServiceOrderGetResponse> ApproveAsync(Guid id)
        {
            var serviceOrder = await FindEntityAsync(id);

            serviceOrder.Approve();

            await _repository.SaveChangesAsync();

            TryRecordMetrics(serviceOrder);

            await TryPublishNotificationAsync(
                serviceOrder.CustomerId,
                serviceOrder.Id,
                serviceOrder.GetCurrentStatus().ToString());

            return MapResponse(serviceOrder);
        }

        public async Task<ServiceOrderGetResponse> RejectAsync(Guid id)
        {
            var serviceOrder = await FindEntityAsync(id);

            serviceOrder.Reject();

            await _repository.SaveChangesAsync();

            TryRecordMetrics(serviceOrder);

            await TryPublishNotificationAsync(
                serviceOrder.CustomerId,
                serviceOrder.Id,
                serviceOrder.GetCurrentStatus().ToString());

            return MapResponse(serviceOrder);
        }

        public async Task<ServiceOrderGetResponse> FinishExecutionAsync(
            Guid id,
            bool hasPendingParts = false)
        {
            var serviceOrder = await FindEntityAsync(id);

            serviceOrder.FinishExecution(hasPendingParts);

            await _repository.SaveChangesAsync();

            TryRecordMetrics(serviceOrder);

            await TryPublishNotificationAsync(
                serviceOrder.CustomerId,
                serviceOrder.Id,
                serviceOrder.GetCurrentStatus().ToString());

            return MapResponse(serviceOrder);
        }

        public async Task<ServiceOrderGetResponse> DeliverAsync(Guid id)
        {
            var serviceOrder = await FindEntityAsync(id);

            serviceOrder.Deliver();

            await _repository.SaveChangesAsync();

            TryRecordMetrics(serviceOrder);

            await TryPublishNotificationAsync(
                serviceOrder.CustomerId,
                serviceOrder.Id,
                serviceOrder.GetCurrentStatus().ToString());

            return MapResponse(serviceOrder);
        }

        private async Task<ServiceOrder> FindEntityAsync(Guid id)
        {
            return await _repository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException(
                    $"Service order {id} was not found.");
        }

        private void TryRecordMetrics(ServiceOrder serviceOrder)
        {
            if (_metrics == null) return;

            try
            {
                _metrics.CalculateServiceOrderStatusMeanTimeMetric(serviceOrder);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to record metrics for ServiceOrder {ServiceOrderId}", serviceOrder.Id);
            }
        }

        private async Task TryPublishNotificationAsync(Guid customerId, Guid serviceOrderId, string status)
        {
            if (_notificationPublisher == null) return;

            try
            {
                await _notificationPublisher.PublishNotificationAsync(customerId, serviceOrderId, status);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to publish notification for ServiceOrder {ServiceOrderId}", serviceOrderId);
            }
        }

        #endregion
    }
}
