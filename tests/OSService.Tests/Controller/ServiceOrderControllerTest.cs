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
    }
}
