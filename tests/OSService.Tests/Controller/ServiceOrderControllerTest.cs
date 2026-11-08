using Microsoft.AspNetCore.Mvc;
using Moq;
using OSService.Api.Controllers;
using OSService.Application.Commands;
using OSService.Application.Services;
using OSService.Domain.Entities;
using OSService.Domain.Exceptions;
using OSService.Domain.Interfaces;
using Xunit;

namespace OSService.Tests.Controllers
{
    public class ServiceOrderControllerTest
    {
        private readonly Mock<IServiceOrderRepository> _repositoryMock;
        private readonly ServiceOrderController _controller;

        public ServiceOrderControllerTest()
        {
            _repositoryMock = new Mock<IServiceOrderRepository>();

            var service = new ServiceOrderService(
                _repositoryMock.Object);

            _controller = new ServiceOrderController(service);
        }

        private static ServiceOrder CreateServiceOrder()
        {
            return new ServiceOrder(
                Guid.NewGuid(),
                Guid.NewGuid());
        }

        private void ConfigureServiceOrder(ServiceOrder serviceOrder)
        {
            _repositoryMock
                .Setup(x => x.GetByIdAsync(serviceOrder.Id))
                .ReturnsAsync(serviceOrder);
        }

        [Fact]
        public async Task Create_ShouldReturnCreated()
        {
            var request = new ServiceOrderRequest
            {
                CustomerId = Guid.NewGuid(),
                VehicleId = Guid.NewGuid()
            };

            var result = await _controller.Create(request);

            var createdResult =
                Assert.IsType<CreatedAtActionResult>(result);

            Assert.Equal(201, createdResult.StatusCode);
            Assert.Equal(
                nameof(ServiceOrderController.GetById),
                createdResult.ActionName);

            var response =
                Assert.IsType<ServiceOrderGetResponse>(
                    createdResult.Value);

            Assert.Equal(request.CustomerId, response.CustomerId);
            Assert.Equal(request.VehicleId, response.VehicleId);

            _repositoryMock.Verify(
                x => x.AddAsync(It.IsAny<ServiceOrder>()),
                Times.Once);

            _repositoryMock.Verify(
                x => x.SaveChangesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task Create_WithInvalidCustomer_ShouldReturnBadRequest()
        {
            var request = new ServiceOrderRequest
            {
                CustomerId = Guid.Empty,
                VehicleId = Guid.NewGuid()
            };

            var result = await _controller.Create(request);

            Assert.IsType<BadRequestObjectResult>(result);

            _repositoryMock.Verify(
                x => x.AddAsync(It.IsAny<ServiceOrder>()),
                Times.Never);
        }

        [Fact]
        public async Task GetById_WhenFound_ShouldReturnOk()
        {
            var serviceOrder = CreateServiceOrder();

            ConfigureServiceOrder(serviceOrder);

            var result = await _controller.GetById(serviceOrder.Id);

            var okResult = Assert.IsType<OkObjectResult>(result);

            var response =
                Assert.IsType<ServiceOrderGetResponse>(
                    okResult.Value);

            Assert.Equal(serviceOrder.Id, response.Id);
        }

        [Fact]
        public async Task GetById_WhenNotFound_ShouldReturnNotFound()
        {
            var id = Guid.NewGuid();

            _repositoryMock
                .Setup(x => x.GetByIdAsync(id))
                .ReturnsAsync((ServiceOrder?)null);

            var result = await _controller.GetById(id);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetAll_ShouldReturnOk()
        {
            var serviceOrders = new List<ServiceOrder>
            {
                CreateServiceOrder(),
                CreateServiceOrder()
            };

            _repositoryMock
                .Setup(x => x.GetAllAsync())
                .ReturnsAsync(serviceOrders);

            var result = await _controller.GetAll();

            var okResult = Assert.IsType<OkObjectResult>(result);

            var response =
                Assert.IsType<List<ServiceOrderGetResponse>>(
                    okResult.Value);

            Assert.Equal(2, response.Count);
        }

        [Fact]
        public async Task GetAll_WhenEmpty_ShouldReturnEmptyList()
        {
            _repositoryMock
                .Setup(x => x.GetAllAsync())
                .ReturnsAsync(new List<ServiceOrder>());

            var result = await _controller.GetAll();

            var okResult = Assert.IsType<OkObjectResult>(result);

            var response =
                Assert.IsType<List<ServiceOrderGetResponse>>(
                    okResult.Value);

            Assert.Empty(response);
        }

        [Fact]
        public async Task StartDiagnostics_ShouldReturnOk()
        {
            var serviceOrder = CreateServiceOrder();

            ConfigureServiceOrder(serviceOrder);

            var result = await _controller.StartDiagnostics(
                serviceOrder.Id);

            Assert.IsType<OkObjectResult>(result);

            _repositoryMock.Verify(
                x => x.SaveChangesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task StartDiagnostics_WhenNotFound_ShouldReturnNotFound()
        {
            var result = await _controller.StartDiagnostics(
                Guid.NewGuid());

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task StartDiagnostics_WhenInvalidStatus_ShouldReturnBadRequest()
        {
            var serviceOrder = CreateServiceOrder();

            serviceOrder.StartDiagnostics();

            ConfigureServiceOrder(serviceOrder);

            var result = await _controller.StartDiagnostics(
                serviceOrder.Id);

            Assert.IsType<BadRequestObjectResult>(result);

            _repositoryMock.Verify(
                x => x.SaveChangesAsync(),
                Times.Never);
        }

        [Fact]
        public async Task FinishAnalysis_ShouldReturnOk()
        {
            var serviceOrder = CreateServiceOrder();

            serviceOrder.StartDiagnostics();

            ConfigureServiceOrder(serviceOrder);

            var request = new ServiceOrderFinishAnalysisRequest
            {
                Budget = 350m
            };

            var result = await _controller.FinishAnalysis(
                serviceOrder.Id,
                request);

            var okResult = Assert.IsType<OkObjectResult>(result);

            var response =
                Assert.IsType<ServiceOrderGetResponse>(
                    okResult.Value);

            Assert.Equal(350m, response.Budget);
        }

        [Fact]
        public async Task FinishAnalysis_WithNegativeBudget_ShouldReturnBadRequest()
        {
            var serviceOrder = CreateServiceOrder();

            serviceOrder.StartDiagnostics();

            ConfigureServiceOrder(serviceOrder);

            var request = new ServiceOrderFinishAnalysisRequest
            {
                Budget = -10m
            };

            var result = await _controller.FinishAnalysis(
                serviceOrder.Id,
                request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Approve_ShouldReturnOk()
        {
            var serviceOrder = CreateServiceOrder();

            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(350m);

            ConfigureServiceOrder(serviceOrder);

            var result = await _controller.Approve(serviceOrder.Id);

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task Approve_WhenNotFound_ShouldReturnNotFound()
        {
            var result = await _controller.Approve(Guid.NewGuid());

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task Reject_ShouldReturnOk()
        {
            var serviceOrder = CreateServiceOrder();

            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(350m);

            ConfigureServiceOrder(serviceOrder);

            var result = await _controller.Reject(serviceOrder.Id);

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task Reject_WhenInvalidStatus_ShouldReturnBadRequest()
        {
            var serviceOrder = CreateServiceOrder();

            ConfigureServiceOrder(serviceOrder);

            var result = await _controller.Reject(serviceOrder.Id);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task FinishExecution_ShouldReturnOk()
        {
            var serviceOrder = CreateServiceOrder();

            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(350m);
            serviceOrder.Approve();
            serviceOrder.ConfirmPayment("TX-100");

            ConfigureServiceOrder(serviceOrder);

            var result = await _controller.FinishExecution(serviceOrder.Id, false);

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task FinishExecution_WithPendingParts_ShouldReturnBadRequest()
        {
            var serviceOrder = CreateServiceOrder();

            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(350m);
            serviceOrder.Approve();
            serviceOrder.ConfirmPayment("TX-100");

            ConfigureServiceOrder(serviceOrder);

            var result = await _controller.FinishExecution(serviceOrder.Id, true);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Deliver_ShouldReturnOk()
        {
            var serviceOrder = CreateServiceOrder();

            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(350m);
            serviceOrder.Approve();
            serviceOrder.ConfirmPayment("TX-100");
            serviceOrder.FinishExecution(false);

            ConfigureServiceOrder(serviceOrder);

            var result = await _controller.Deliver(serviceOrder.Id);

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task Deliver_WhenInvalidStatus_ShouldReturnBadRequest()
        {
            var serviceOrder = CreateServiceOrder();

            ConfigureServiceOrder(serviceOrder);

            var result = await _controller.Deliver(serviceOrder.Id);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Deliver_WhenNotFound_ShouldReturnNotFound()
        {
            var result = await _controller.Deliver(Guid.NewGuid());

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetByStatus_ShouldReturnOk()
        {
            var serviceOrder = CreateServiceOrder();
            _repositoryMock
                .Setup(x => x.GetByStatusAsync(OSService.Domain.Enum.OrderStatus.Received))
                .ReturnsAsync(new List<ServiceOrder> { serviceOrder });

            var result = await _controller.GetByStatus(OSService.Domain.Enum.OrderStatus.Received);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<List<ServiceOrderGetResponse>>(okResult.Value);
            Assert.Single(response);
        }

        [Fact]
        public async Task GetAll_WithStatus_ShouldReturnOk()
        {
            var serviceOrder = CreateServiceOrder();
            _repositoryMock
                .Setup(x => x.GetAllAsync(OSService.Domain.Enum.OrderStatus.Received))
                .ReturnsAsync(new List<ServiceOrder> { serviceOrder });

            var result = await _controller.GetAll(OSService.Domain.Enum.OrderStatus.Received);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<List<ServiceOrderGetResponse>>(okResult.Value);
            Assert.Single(response);
        }

        [Fact]
        public async Task AddPart_ShouldReturnOk()
        {
            var serviceOrder = CreateServiceOrder();
            ConfigureServiceOrder(serviceOrder);

            var request = new ServiceOrderAddPartRequest
            {
                PartId = Guid.NewGuid(),
                PartName = "Brake Disc",
                Quantity = 2,
                UnitPrice = 120m
            };

            var result = await _controller.AddPart(serviceOrder.Id, request);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
            Assert.Single(response.Parts);
            Assert.Equal(240m, response.Parts.First().TotalPrice);
        }

        [Fact]
        public async Task AddPart_WhenInvalidStatus_ShouldReturnBadRequest()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(100m);
            serviceOrder.Approve();
            ConfigureServiceOrder(serviceOrder);

            var request = new ServiceOrderAddPartRequest
            {
                PartId = Guid.NewGuid(),
                Quantity = 1,
                UnitPrice = 50m
            };

            var result = await _controller.AddPart(serviceOrder.Id, request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task RemovePart_ShouldReturnOk()
        {
            var serviceOrder = CreateServiceOrder();
            var partId = Guid.NewGuid();
            serviceOrder.AddPart(partId, 1, 50m);
            ConfigureServiceOrder(serviceOrder);

            var result = await _controller.RemovePart(serviceOrder.Id, partId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
            Assert.Empty(response.Parts);
        }

        [Fact]
        public async Task RemovePart_WhenNotFound_ShouldReturnBadRequest()
        {
            var serviceOrder = CreateServiceOrder();
            ConfigureServiceOrder(serviceOrder);

            var result = await _controller.RemovePart(serviceOrder.Id, Guid.NewGuid());

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task AddService_ShouldReturnOk()
        {
            var serviceOrder = CreateServiceOrder();
            ConfigureServiceOrder(serviceOrder);

            var request = new ServiceOrderAddServiceRequest
            {
                ServiceId = Guid.NewGuid(),
                ServiceName = "General Maintenance",
                Price = 300m
            };

            var result = await _controller.AddService(serviceOrder.Id, request);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
            Assert.Single(response.Services);
            Assert.Equal(300m, response.Services.First().Price);
        }

        [Fact]
        public async Task AddService_WhenDuplicate_ShouldReturnBadRequest()
        {
            var serviceOrder = CreateServiceOrder();
            var serviceId = Guid.NewGuid();
            serviceOrder.AddService(serviceId, 100m);
            ConfigureServiceOrder(serviceOrder);

            var request = new ServiceOrderAddServiceRequest
            {
                ServiceId = serviceId,
                Price = 120m
            };

            var result = await _controller.AddService(serviceOrder.Id, request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task RemoveService_ShouldReturnOk()
        {
            var serviceOrder = CreateServiceOrder();
            var serviceId = Guid.NewGuid();
            serviceOrder.AddService(serviceId, 100m);
            ConfigureServiceOrder(serviceOrder);

            var result = await _controller.RemoveService(serviceOrder.Id, serviceId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
            Assert.Empty(response.Services);
        }

        [Fact]
        public async Task RemoveService_WhenNotFound_ShouldReturnBadRequest()
        {
            var serviceOrder = CreateServiceOrder();
            ConfigureServiceOrder(serviceOrder);

            var result = await _controller.RemoveService(serviceOrder.Id, Guid.NewGuid());

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreateBudget_ShouldReturnOk()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.AddPart(Guid.NewGuid(), 2, 50m);
            serviceOrder.AddService(Guid.NewGuid(), 100m);
            ConfigureServiceOrder(serviceOrder);

            var result = await _controller.CreateBudget(serviceOrder.Id, new ServiceOrderCreateBudgetRequest());

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
            Assert.Equal(200m, response.Budget);
        }

        [Fact]
        public async Task ReceivePayment_ShouldReturnOk()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(250m);
            ConfigureServiceOrder(serviceOrder);

            var request = new ServiceOrderPaymentRequest
            {
                Amount = 250m,
                TransactionId = "PAY-54321"
            };

            var result = await _controller.ReceivePayment(serviceOrder.Id, request);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ServiceOrderGetResponse>(okResult.Value);
            Assert.True(response.IsPaid);
            Assert.Equal(250m, response.PaidAmount);
            Assert.Equal(OSService.Domain.Enum.OrderStatus.InProgress, response.Status);
        }

        [Fact]
        public async Task ReceivePayment_WhenInvalid_ShouldReturnBadRequest()
        {
            var serviceOrder = CreateServiceOrder();
            ConfigureServiceOrder(serviceOrder);

            var request = new ServiceOrderPaymentRequest { Amount = 100m };

            var result = await _controller.ReceivePayment(serviceOrder.Id, request);

            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
