
using Moq;
using OSService.Application.Commands;
using OSService.Application.Services;
using OSService.Domain.Entities;
using OSService.Domain.Enum;
using OSService.Domain.Interfaces;
using Xunit;

namespace OSService.Tests.Application
{
    public class ServiceOrderServiceTest
    {
        private readonly Mock<IServiceOrderRepository> _repositoryMock;
        private readonly ServiceOrderService _service;

        public ServiceOrderServiceTest()
        {
            _repositoryMock = new Mock<IServiceOrderRepository>();
            _service = new ServiceOrderService(_repositoryMock.Object);
        }

        private static ServiceOrder CreateServiceOrder()
        {
            return new ServiceOrder(Guid.NewGuid(), Guid.NewGuid());
        }

        [Fact]
        public async Task CreateAsync_ShouldCreateServiceOrder()
        {
            var request = new ServiceOrderRequest
            {
                CustomerId = Guid.NewGuid(),
                VehicleId = Guid.NewGuid()
            };

            ServiceOrder? createdOrder = null;

            _repositoryMock
                .Setup(x => x.AddAsync(It.IsAny<ServiceOrder>()))
                .Callback<ServiceOrder>(order => createdOrder = order)
                .Returns(Task.CompletedTask);

            var result = await _service.CreateAsync(request);

            Assert.NotNull(createdOrder);
            Assert.Equal(request.CustomerId, result.CustomerId);
            Assert.Equal(request.VehicleId, result.VehicleId);
            Assert.Equal(OrderStatus.Received, result.Status);

            _repositoryMock.Verify(
                x => x.AddAsync(It.IsAny<ServiceOrder>()),
                Times.Once);

            _repositoryMock.Verify(
                x => x.SaveChangesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnServiceOrder()
        {
            var serviceOrder = CreateServiceOrder();

            _repositoryMock
                .Setup(x => x.GetByIdAsync(serviceOrder.Id))
                .ReturnsAsync(serviceOrder);

            var result = await _service.GetByIdAsync(serviceOrder.Id);

            Assert.NotNull(result);
            Assert.Equal(serviceOrder.Id, result.Id);
            Assert.Equal(OrderStatus.Received, result.Status);
        }

        [Fact]
        public async Task GetByIdAsync_WhenNotFound_ShouldReturnNull()
        {
            var id = Guid.NewGuid();

            _repositoryMock
                .Setup(x => x.GetByIdAsync(id))
                .ReturnsAsync((ServiceOrder?)null);

            var result = await _service.GetByIdAsync(id);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnServiceOrders()
        {
            var serviceOrders = new List<ServiceOrder>
            {
                CreateServiceOrder(),
                CreateServiceOrder()
            };

            _repositoryMock
                .Setup(x => x.GetAllAsync())
                .ReturnsAsync(serviceOrders);

            var result = await _service.GetAllAsync();

            Assert.Equal(2, result.Count);

            Assert.All(result, x =>
                Assert.Equal(OrderStatus.Received, x.Status));
        }

        [Fact]
        public async Task StartDiagnosticsAsync_ShouldChangeStatusAndSave()
        {
            var serviceOrder = CreateServiceOrder();

            _repositoryMock
                .Setup(x => x.GetByIdAsync(serviceOrder.Id))
                .ReturnsAsync(serviceOrder);

            var result = await _service.StartDiagnosticsAsync(serviceOrder.Id);

            Assert.Equal(OrderStatus.UnderAnalysis, result.Status);

            _repositoryMock.Verify(
                x => x.SaveChangesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task FinishAnalysisAsync_ShouldUpdateBudgetAndSave()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();

            _repositoryMock
                .Setup(x => x.GetByIdAsync(serviceOrder.Id))
                .ReturnsAsync(serviceOrder);

            var request = new ServiceOrderFinishAnalysisRequest
            {
                Budget = 350m
            };

            var result = await _service.FinishAnalysisAsync(
                serviceOrder.Id, request);

            Assert.Equal(350m, result.Budget);
            Assert.Equal(
                OrderStatus.AwaitingApproval,
                result.Status);

            _repositoryMock.Verify(
                x => x.SaveChangesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task ApproveAsync_ShouldChangeStatusAndSave()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(350m);

            _repositoryMock
                .Setup(x => x.GetByIdAsync(serviceOrder.Id))
                .ReturnsAsync(serviceOrder);

            var result = await _service.ApproveAsync(serviceOrder.Id);

            Assert.Equal(OrderStatus.InProgress, result.Status);

            _repositoryMock.Verify(
                x => x.SaveChangesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task RejectAsync_ShouldChangeStatusAndSave()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(350m);

            _repositoryMock
                .Setup(x => x.GetByIdAsync(serviceOrder.Id))
                .ReturnsAsync(serviceOrder);

            var result = await _service.RejectAsync(serviceOrder.Id);

            Assert.Equal(OrderStatus.Rejected, result.Status);

            _repositoryMock.Verify(
                x => x.SaveChangesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task FinishExecutionAsync_ShouldChangeStatusAndSave()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(350m);
            serviceOrder.Approve();

            _repositoryMock
                .Setup(x => x.GetByIdAsync(serviceOrder.Id))
                .ReturnsAsync(serviceOrder);

            var result = await _service.FinishExecutionAsync(serviceOrder.Id, false);

            Assert.Equal(OrderStatus.Completed, result.Status);

            _repositoryMock.Verify(
                x => x.SaveChangesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task DeliverAsync_ShouldChangeStatusAndSave()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(350m);
            serviceOrder.Approve();
            serviceOrder.FinishExecution(false);

            _repositoryMock
                .Setup(x => x.GetByIdAsync(serviceOrder.Id))
                .ReturnsAsync(serviceOrder);

            var result = await _service.DeliverAsync(serviceOrder.Id);

            Assert.Equal(OrderStatus.Delivered, result.Status);

            _repositoryMock.Verify(
                x => x.SaveChangesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task StartDiagnosticsAsync_WhenNotFound_ShouldThrowException()
        {
            var id = Guid.NewGuid();

            _repositoryMock
                .Setup(x => x.GetByIdAsync(id))
                .ReturnsAsync((ServiceOrder?)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _service.StartDiagnosticsAsync(id));

            _repositoryMock.Verify(
                x => x.SaveChangesAsync(),
                Times.Never);
        }

        [Fact]
        public async Task CreateAsync_WhenPublisherConfigured_ShouldPublishStatusNotification()
        {
            var publisherMock = new Mock<OSService.Domain.Interface.IServiceOrderNotificationPublisher>();
            var serviceWithPublisher = new ServiceOrderService(
                _repositoryMock.Object,
                publisherMock.Object);

            var customerId = Guid.NewGuid();
            var request = new ServiceOrderRequest
            {
                CustomerId = customerId,
                VehicleId = Guid.NewGuid()
            };

            _repositoryMock
                .Setup(x => x.AddAsync(It.IsAny<ServiceOrder>()))
                .Returns(Task.CompletedTask);

            var result = await serviceWithPublisher.CreateAsync(request);

            Assert.NotNull(result);
            publisherMock.Verify(
                x => x.PublishNotificationAsync(customerId, result.Id, "Received", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateAsync_WhenPublisherThrows_ShouldNotThrowAndCreateServiceOrder()
        {
            var publisherMock = new Mock<OSService.Domain.Interface.IServiceOrderNotificationPublisher>();
            publisherMock
                .Setup(x => x.PublishNotificationAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("SQS is down"));

            var serviceWithPublisher = new ServiceOrderService(
                _repositoryMock.Object,
                publisherMock.Object);

            var customerId = Guid.NewGuid();
            var request = new ServiceOrderRequest
            {
                CustomerId = customerId,
                VehicleId = Guid.NewGuid()
            };

            _repositoryMock
                .Setup(x => x.AddAsync(It.IsAny<ServiceOrder>()))
                .Returns(Task.CompletedTask);

            var result = await serviceWithPublisher.CreateAsync(request);

            Assert.NotNull(result);
            Assert.Equal(OrderStatus.Received, result.Status);
        }

        [Fact]
        public async Task StartDiagnosticsAsync_WhenMetricsConfigured_ShouldCallCalculateMetric()
        {
            var metricsMock = new Mock<OSService.Domain.Interface.IServiceOrderMetrics>();
            var serviceWithMetrics = new ServiceOrderService(
                _repositoryMock.Object,
                notificationPublisher: null,
                logger: null,
                metrics: metricsMock.Object);

            var serviceOrder = CreateServiceOrder();
            _repositoryMock
                .Setup(x => x.GetByIdAsync(serviceOrder.Id))
                .ReturnsAsync(serviceOrder);

            await serviceWithMetrics.StartDiagnosticsAsync(serviceOrder.Id);

            metricsMock.Verify(
                x => x.CalculateServiceOrderStatusMeanTimeMetric(It.Is<ServiceOrder>(o => o.Id == serviceOrder.Id)),
                Times.Once);
        }

        [Fact]
        public async Task FinishAnalysisAsync_WhenMetricsConfigured_ShouldCallCalculateMetric()
        {
            var metricsMock = new Mock<OSService.Domain.Interface.IServiceOrderMetrics>();
            var serviceWithMetrics = new ServiceOrderService(
                _repositoryMock.Object,
                notificationPublisher: null,
                logger: null,
                metrics: metricsMock.Object);

            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();

            _repositoryMock
                .Setup(x => x.GetByIdAsync(serviceOrder.Id))
                .ReturnsAsync(serviceOrder);

            await serviceWithMetrics.FinishAnalysisAsync(serviceOrder.Id, new ServiceOrderFinishAnalysisRequest { Budget = 200m });

            metricsMock.Verify(
                x => x.CalculateServiceOrderStatusMeanTimeMetric(It.Is<ServiceOrder>(o => o.Id == serviceOrder.Id)),
                Times.Once);
        }

        [Fact]
        public async Task StatusChange_WhenMetricsThrows_ShouldNotThrowAndCompleteSuccessfully()
        {
            var metricsMock = new Mock<OSService.Domain.Interface.IServiceOrderMetrics>();
            metricsMock
                .Setup(x => x.CalculateServiceOrderStatusMeanTimeMetric(It.IsAny<ServiceOrder>()))
                .Throws(new InvalidOperationException("Metrics pipeline error"));

            var serviceWithMetrics = new ServiceOrderService(
                _repositoryMock.Object,
                notificationPublisher: null,
                logger: null,
                metrics: metricsMock.Object);

            var serviceOrder = CreateServiceOrder();
            _repositoryMock
                .Setup(x => x.GetByIdAsync(serviceOrder.Id))
                .ReturnsAsync(serviceOrder);

            var result = await serviceWithMetrics.StartDiagnosticsAsync(serviceOrder.Id);

            Assert.NotNull(result);
            Assert.Equal(OrderStatus.UnderAnalysis, result.Status);
        }

        [Fact]
        public async Task GetByStatusAsync_ShouldReturnFilteredOrders()
        {
            var serviceOrder = CreateServiceOrder();

            _repositoryMock
                .Setup(x => x.GetByStatusAsync(OrderStatus.Received))
                .ReturnsAsync(new List<ServiceOrder> { serviceOrder });

            var result = await _service.GetByStatusAsync(OrderStatus.Received);

            Assert.Single(result);
            Assert.Equal(serviceOrder.Id, result.First().Id);
        }

        [Fact]
        public async Task GetAllAsync_WithStatus_ShouldReturnFilteredOrders()
        {
            var serviceOrder = CreateServiceOrder();

            _repositoryMock
                .Setup(x => x.GetAllAsync(OrderStatus.Received))
                .ReturnsAsync(new List<ServiceOrder> { serviceOrder });

            var result = await _service.GetAllAsync(OrderStatus.Received);

            Assert.Single(result);
            Assert.Equal(serviceOrder.Id, result.First().Id);
        }

        [Fact]
        public async Task AddPartAsync_ShouldAddPartAndSave()
        {
            var serviceOrder = CreateServiceOrder();
            _repositoryMock.Setup(x => x.GetByIdAsync(serviceOrder.Id)).ReturnsAsync(serviceOrder);

            var partRequest = new ServiceOrderAddPartRequest
            {
                PartId = Guid.NewGuid(),
                PartName = "Filter",
                Quantity = 2,
                UnitPrice = 35m
            };

            var result = await _service.AddPartAsync(serviceOrder.Id, partRequest);

            Assert.Single(result.Parts);
            Assert.Equal(partRequest.PartId, result.Parts.First().PartId);
            Assert.Equal(70m, result.Parts.First().TotalPrice);
            _repositoryMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task RemovePartAsync_ShouldRemovePartAndSave()
        {
            var serviceOrder = CreateServiceOrder();
            var partId = Guid.NewGuid();
            serviceOrder.AddPart(partId, 1, 50m);
            _repositoryMock.Setup(x => x.GetByIdAsync(serviceOrder.Id)).ReturnsAsync(serviceOrder);

            var result = await _service.RemovePartAsync(serviceOrder.Id, partId);

            Assert.Empty(result.Parts);
            _repositoryMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task AddServiceAsync_ShouldAddServiceAndSave()
        {
            var serviceOrder = CreateServiceOrder();
            _repositoryMock.Setup(x => x.GetByIdAsync(serviceOrder.Id)).ReturnsAsync(serviceOrder);

            var serviceRequest = new ServiceOrderAddServiceRequest
            {
                ServiceId = Guid.NewGuid(),
                ServiceName = "Alignment",
                Price = 120m
            };

            var result = await _service.AddServiceAsync(serviceOrder.Id, serviceRequest);

            Assert.Single(result.Services);
            Assert.Equal(serviceRequest.ServiceId, result.Services.First().ServiceId);
            Assert.Equal(120m, result.Services.First().Price);
            _repositoryMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task RemoveServiceAsync_ShouldRemoveServiceAndSave()
        {
            var serviceOrder = CreateServiceOrder();
            var serviceId = Guid.NewGuid();
            serviceOrder.AddService(serviceId, 100m);
            _repositoryMock.Setup(x => x.GetByIdAsync(serviceOrder.Id)).ReturnsAsync(serviceOrder);

            var result = await _service.RemoveServiceAsync(serviceOrder.Id, serviceId);

            Assert.Empty(result.Services);
            _repositoryMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task CreateBudgetAsync_ShouldCreateBudgetAndSave()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.AddPart(Guid.NewGuid(), 2, 50m);
            serviceOrder.AddService(Guid.NewGuid(), 100m);
            _repositoryMock.Setup(x => x.GetByIdAsync(serviceOrder.Id)).ReturnsAsync(serviceOrder);

            var result = await _service.CreateBudgetAsync(serviceOrder.Id);

            Assert.Equal(200m, result.Budget);
            _repositoryMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ReceivePaymentAsync_ShouldRecordPaymentAndSave()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(300m);
            _repositoryMock.Setup(x => x.GetByIdAsync(serviceOrder.Id)).ReturnsAsync(serviceOrder);

            var paymentRequest = new ServiceOrderPaymentRequest
            {
                Amount = 300m,
                TransactionId = "MP-987"
            };

            var result = await _service.ReceivePaymentAsync(serviceOrder.Id, paymentRequest);

            Assert.True(result.IsPaid);
            Assert.Equal(300m, result.PaidAmount);
            Assert.Equal("MP-987", result.PaymentTransactionId);
            Assert.Equal(OrderStatus.InProgress, result.Status);
            _repositoryMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task EnsurePartStockAsync_ShouldEnsureStockAndSave()
        {
            var serviceOrder = CreateServiceOrder();
            var partId = Guid.NewGuid();
            serviceOrder.AddPart(partId, 1, 50m, stockQuantityWasEnsured: false);
            _repositoryMock.Setup(x => x.GetByIdAsync(serviceOrder.Id)).ReturnsAsync(serviceOrder);

            var result = await _service.EnsurePartStockAsync(serviceOrder.Id, partId);

            Assert.True(result.Parts.First().StockQuantityWasEnsured);
            _repositoryMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        }
    }
}
