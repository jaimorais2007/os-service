
using OSService.Domain.Entities;
using OSService.Domain.Enum;
using OSService.Domain.Exceptions;
using Xunit;

namespace OSService.Tests.Domain
{
    public class ServiceOrderTest
    {
        private static ServiceOrder CreateServiceOrder()
        {
            return new ServiceOrder(
                Guid.NewGuid(),
                Guid.NewGuid());
        }

        [Fact]
        public void CreateServiceOrder_ShouldStartWithReceivedStatus()
        {
            var serviceOrder = CreateServiceOrder();

            Assert.Equal(
                OrderStatus.Received,
                serviceOrder.GetCurrentStatus());

            Assert.Single(serviceOrder.StatusHistory);
            Assert.False(serviceOrder.Inactive);
        }

        [Fact]
        public void StartDiagnostics_ShouldChangeStatusToUnderAnalysis()
        {
            var serviceOrder = CreateServiceOrder();

            serviceOrder.StartDiagnostics();

            Assert.Equal(
                OrderStatus.UnderAnalysis,
                serviceOrder.GetCurrentStatus());

            Assert.Equal(2, serviceOrder.StatusHistory.Count);
        }

        [Fact]
        public void FinishAnalysis_ShouldUpdateBudgetAndStatus()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();

            serviceOrder.FinishAnalysis(350m);

            Assert.Equal(350m, serviceOrder.Budget);

            Assert.Equal(
                OrderStatus.AwaitingApproval,
                serviceOrder.GetCurrentStatus());
        }

        [Fact]
        public void Approve_ShouldChangeStatusToInProgress()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(350m);

            serviceOrder.Approve();

            Assert.Equal(
                OrderStatus.InProgress,
                serviceOrder.GetCurrentStatus());
        }

        [Fact]
        public void Reject_ShouldChangeStatusToRejected()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(350m);

            serviceOrder.Reject();

            Assert.Equal(
                OrderStatus.Rejected,
                serviceOrder.GetCurrentStatus());
        }

        [Fact]
        public void Deliver_WithoutFinishingExecution_ShouldThrowException()
        {
            var serviceOrder = CreateServiceOrder();

            var exception = Assert.Throws<DomainException>(
                () => serviceOrder.Deliver());

            Assert.Contains(
                "Completed",
                exception.Message);
        }

        [Fact]
        public void FinishAnalysis_WithNegativeBudget_ShouldThrowException()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();

            Assert.Throws<DomainException>(
                () => serviceOrder.FinishAnalysis(-10m));
        }

        [Fact]
        public void CreateServiceOrder_WithoutCustomer_ShouldThrowException()
        {
            Assert.Throws<DomainException>(
                () => new ServiceOrder(
                    Guid.Empty,
                    Guid.NewGuid()));
        }

        [Fact]
        public void FinishExecution_WithPendingParts_ShouldThrowException()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(350m);
            serviceOrder.Approve();

            Assert.Throws<DomainException>(
                () => serviceOrder.FinishExecution(true));
        }

        [Fact]
        public void FinishExecution_WithoutPendingParts_ShouldComplete()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(350m);
            serviceOrder.Approve();

            serviceOrder.FinishExecution(false);

            Assert.Equal(
                OrderStatus.Completed,
                serviceOrder.GetCurrentStatus());
        }

        [Fact]
        public void Deliver_AfterFinishingExecution_ShouldChangeStatusToDelivered()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(350m);
            serviceOrder.Approve();
            serviceOrder.FinishExecution(false);

            serviceOrder.Deliver();

            Assert.Equal(
                OrderStatus.Delivered,
                serviceOrder.GetCurrentStatus());
        }
    }
}
