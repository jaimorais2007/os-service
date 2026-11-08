using Microsoft.Extensions.Logging;
using OSService.Application.Commands;
using OSService.Application.Saga;
using OSService.Domain.Entities;
using OSService.Domain.Enum;
using OSService.Domain.Exceptions;
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
        private readonly ISagaEventPublisher? _sagaEventPublisher;

        public ServiceOrderService(
            IServiceOrderRepository repository,
            IServiceOrderNotificationPublisher? notificationPublisher = null,
            ILogger<ServiceOrderService>? logger = null,
            IServiceOrderMetrics? metrics = null,
            ISagaEventPublisher? sagaEventPublisher = null)
        {
            _repository = repository;
            _notificationPublisher = notificationPublisher;
            _logger = logger;
            _metrics = metrics;
            _sagaEventPublisher = sagaEventPublisher;
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

        public async Task<List<ServiceOrderGetResponse>> GetAllAsync(OrderStatus? status)
        {
            if (!status.HasValue)
                return await GetAllAsync();

            var serviceOrders = await _repository.GetAllAsync(status);

            return serviceOrders
                .Select(MapResponse)
                .ToList();
        }

        public async Task<List<ServiceOrderGetResponse>> GetByStatusAsync(OrderStatus status)
        {
            var serviceOrders = await _repository.GetByStatusAsync(status);

            return serviceOrders
                .Select(MapResponse)
                .ToList();
        }

        #region Service Order Parts and Services

        public async Task<ServiceOrderGetResponse> AddPartAsync(
            Guid id,
            ServiceOrderAddPartRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var serviceOrder = await FindEntityAsync(id);

            serviceOrder.AddPart(
                request.PartId,
                request.Quantity,
                request.UnitPrice,
                request.PartName,
                request.StockQuantityWasEnsured);

            await _repository.SaveChangesAsync();

            _logger?.LogInformation(
                "Part {PartId} added to Service Order {ServiceOrderId}",
                request.PartId,
                id);

            return MapResponse(serviceOrder);
        }

        public async Task<ServiceOrderGetResponse> RemovePartAsync(
            Guid id,
            Guid partId)
        {
            var serviceOrder = await FindEntityAsync(id);

            serviceOrder.RemovePart(partId);

            await _repository.SaveChangesAsync();

            _logger?.LogInformation(
                "Part {PartId} removed from Service Order {ServiceOrderId}",
                partId,
                id);

            return MapResponse(serviceOrder);
        }

        public async Task<ServiceOrderGetResponse> EnsurePartStockAsync(
            Guid id,
            Guid partId)
        {
            var serviceOrder = await FindEntityAsync(id);

            serviceOrder.EnsurePartStock(partId);

            await _repository.SaveChangesAsync();

            return MapResponse(serviceOrder);
        }

        public async Task<ServiceOrderGetResponse> AddServiceAsync(
            Guid id,
            ServiceOrderAddServiceRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var serviceOrder = await FindEntityAsync(id);

            serviceOrder.AddService(
                request.ServiceId,
                request.Price,
                request.ServiceName);

            await _repository.SaveChangesAsync();

            _logger?.LogInformation(
                "Service {ServiceId} added to Service Order {ServiceOrderId}",
                request.ServiceId,
                id);

            return MapResponse(serviceOrder);
        }

        public async Task<ServiceOrderGetResponse> RemoveServiceAsync(
            Guid id,
            Guid serviceId)
        {
            var serviceOrder = await FindEntityAsync(id);

            serviceOrder.RemoveService(serviceId);

            await _repository.SaveChangesAsync();

            _logger?.LogInformation(
                "Service {ServiceId} removed from Service Order {ServiceOrderId}",
                serviceId,
                id);

            return MapResponse(serviceOrder);
        }

        #endregion

        #region Service Order Status Changes and Budget

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

        public async Task<ServiceOrderGetResponse> CreateBudgetAsync(
            Guid id,
            decimal? budget = null)
        {
            var serviceOrder = await FindEntityAsync(id);

            serviceOrder.CreateBudget(budget);

            await _repository.SaveChangesAsync();

            _logger?.LogInformation(
                "Budget created for Service Order {ServiceOrderId}. Budget: {Budget}",
                id,
                serviceOrder.Budget);

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

            if (_sagaEventPublisher != null)
            {
                try
                {
                    var totalAmount = serviceOrder.Budget > 0
                        ? serviceOrder.Budget
                        : serviceOrder.CalculateBudget();

                    var payload = new BudgetApprovedPayload(
                        serviceOrder.Id,
                        totalAmount,
                        serviceOrder.Services.Select(s => s.ServiceId).ToList(),
                        serviceOrder.Parts.Select(p => new BudgetApprovedPart(p.PartId, p.Quantity)).ToList());

                    await _sagaEventPublisher.PublishAsync(
                        SagaEventTypes.BudgetApproved,
                        serviceOrder.Id,
                        payload);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(
                        ex,
                        "Failed to publish BudgetApproved event for ServiceOrder {ServiceOrderId}. Reverting approval.",
                        serviceOrder.Id);

                    serviceOrder.RevertToAwaitingApproval();
                    await _repository.SaveChangesAsync();

                    throw new DomainException(
                        "Failed to initiate payment generation for the approved service order. Approval was reverted.");
                }
            }

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

            var previousStatus = serviceOrder.GetCurrentStatus();

            serviceOrder.Reject();

            await _repository.SaveChangesAsync();

            if (previousStatus == OrderStatus.WaitingPayment && _sagaEventPublisher != null)
            {
                try
                {
                    await _sagaEventPublisher.PublishAsync(
                        SagaEventTypes.ServiceOrderRejected,
                        serviceOrder.Id,
                        new ServiceOrderRejectedPayload(serviceOrder.Id));
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(
                        ex,
                        "Failed to publish ServiceOrderRejected event for ServiceOrder {ServiceOrderId}",
                        serviceOrder.Id);
                }
            }

            TryRecordMetrics(serviceOrder);

            await TryPublishNotificationAsync(
                serviceOrder.CustomerId,
                serviceOrder.Id,
                serviceOrder.GetCurrentStatus().ToString());

            return MapResponse(serviceOrder);
        }

        public async Task<ServiceOrderGetResponse> ReceivePaymentAsync(
            Guid id,
            ServiceOrderPaymentRequest? request = null)
        {
            var serviceOrder = await FindEntityAsync(id);

            var previousStatus = serviceOrder.GetCurrentStatus();

            serviceOrder.ReceivePayment(request?.Amount, request?.TransactionId);

            await _repository.SaveChangesAsync();

            if (previousStatus == OrderStatus.WaitingPayment &&
                serviceOrder.Parts.Count > 0 &&
                serviceOrder.Parts.Any(p => !p.StockQuantityWasEnsured) &&
                _sagaEventPublisher != null)
            {
                await _sagaEventPublisher.PublishAsync(
                    SagaEventTypes.ServiceOrderApproved,
                    serviceOrder.Id,
                    new ServiceOrderApprovedPayload(
                        serviceOrder.Id,
                        serviceOrder.Parts.Select(p => new ServiceOrderApprovedPart(p.PartId, p.Quantity)).ToList()));
            }

            if (serviceOrder.GetCurrentStatus() != previousStatus)
            {
                TryRecordMetrics(serviceOrder);

                await TryPublishNotificationAsync(
                    serviceOrder.CustomerId,
                    serviceOrder.Id,
                    serviceOrder.GetCurrentStatus().ToString());
            }

            _logger?.LogInformation(
                "Payment received for Service Order {ServiceOrderId}. Amount: {Amount}",
                id,
                serviceOrder.PaidAmount);

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
                Status = serviceOrder.GetCurrentStatus(),
                IsPaid = serviceOrder.IsPaid,
                PaidAt = serviceOrder.PaidAt,
                PaidAmount = serviceOrder.PaidAmount,
                PaymentTransactionId = serviceOrder.PaymentTransactionId,
                PaymentUrl = serviceOrder.PaymentUrl,
                Parts = serviceOrder.Parts.Select(p => new ServiceOrderPartResponse
                {
                    Id = p.Id,
                    PartId = p.PartId,
                    PartName = p.PartName,
                    Quantity = p.Quantity,
                    UnitPrice = p.UnitPrice,
                    TotalPrice = p.TotalPrice,
                    StockQuantityWasEnsured = p.StockQuantityWasEnsured
                }).ToList(),
                Services = serviceOrder.Services.Select(s => new ServiceOrderServiceResponse
                {
                    Id = s.Id,
                    ServiceId = s.ServiceId,
                    ServiceName = s.ServiceName,
                    Price = s.Price
                }).ToList()
            };
        }
    }
}
