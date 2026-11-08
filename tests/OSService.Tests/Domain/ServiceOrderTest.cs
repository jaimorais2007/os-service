
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
        public void Approve_ShouldChangeStatusToWaitingPayment()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(350m);

            serviceOrder.Approve();

            Assert.Equal(
                OrderStatus.WaitingPayment,
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
            serviceOrder.ConfirmPayment("TX-100");

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
            serviceOrder.ConfirmPayment("TX-100");

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
            serviceOrder.ConfirmPayment("TX-100");
            serviceOrder.FinishExecution(false);

            serviceOrder.Deliver();

            Assert.Equal(
                OrderStatus.Delivered,
                serviceOrder.GetCurrentStatus());
        }

        [Fact]
        public void AddPart_ShouldAddPartToServiceOrder()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();

            var partId = Guid.NewGuid();
            var part = serviceOrder.AddPart(partId, 2, 50m, "Oil Filter");

            Assert.Single(serviceOrder.Parts);
            Assert.Equal(partId, part.PartId);
            Assert.Equal(2, part.Quantity);
            Assert.Equal(50m, part.UnitPrice);
            Assert.Equal(100m, part.TotalPrice);
            Assert.False(part.StockQuantityWasEnsured);
        }

        [Fact]
        public void AddPart_WhenAlreadyExists_ShouldIncreaseQuantity()
        {
            var serviceOrder = CreateServiceOrder();
            var partId = Guid.NewGuid();

            serviceOrder.AddPart(partId, 2, 50m, "Spark Plug");
            serviceOrder.AddPart(partId, 3, 50m, "Spark Plug");

            Assert.Single(serviceOrder.Parts);
            Assert.Equal(5, serviceOrder.Parts.First().Quantity);
            Assert.Equal(250m, serviceOrder.Parts.First().TotalPrice);
        }

        [Fact]
        public void AddPart_WithInvalidData_ShouldThrowException()
        {
            var serviceOrder = CreateServiceOrder();

            Assert.Throws<DomainException>(() => serviceOrder.AddPart(Guid.Empty, 1, 10m));
            Assert.Throws<DomainException>(() => serviceOrder.AddPart(Guid.NewGuid(), 0, 10m));
            Assert.Throws<DomainException>(() => serviceOrder.AddPart(Guid.NewGuid(), 1, -5m));
        }

        [Fact]
        public void AddPart_InInvalidStatus_ShouldThrowException()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(100m);
            serviceOrder.Approve();

            Assert.Throws<DomainException>(() => serviceOrder.AddPart(Guid.NewGuid(), 1, 10m));
        }

        [Fact]
        public void RemovePart_ShouldRemovePartFromServiceOrder()
        {
            var serviceOrder = CreateServiceOrder();
            var partId = Guid.NewGuid();
            serviceOrder.AddPart(partId, 1, 50m);

            serviceOrder.RemovePart(partId);

            Assert.Empty(serviceOrder.Parts);
        }

        [Fact]
        public void RemovePart_WhenNotFound_ShouldThrowException()
        {
            var serviceOrder = CreateServiceOrder();

            Assert.Throws<DomainException>(() => serviceOrder.RemovePart(Guid.NewGuid()));
        }

        [Fact]
        public void AddService_ShouldAddServiceToServiceOrder()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();

            var serviceId = Guid.NewGuid();
            var serviceItem = serviceOrder.AddService(serviceId, 150m, "Oil Change");

            Assert.Single(serviceOrder.Services);
            Assert.Equal(serviceId, serviceItem.ServiceId);
            Assert.Equal(150m, serviceItem.Price);
            Assert.Equal("Oil Change", serviceItem.ServiceName);
        }

        [Fact]
        public void AddService_WhenAlreadyExists_ShouldThrowException()
        {
            var serviceOrder = CreateServiceOrder();
            var serviceId = Guid.NewGuid();

            serviceOrder.AddService(serviceId, 100m);

            Assert.Throws<DomainException>(() => serviceOrder.AddService(serviceId, 120m));
        }

        [Fact]
        public void AddService_WithInvalidData_ShouldThrowException()
        {
            var serviceOrder = CreateServiceOrder();

            Assert.Throws<DomainException>(() => serviceOrder.AddService(Guid.Empty, 50m));
            Assert.Throws<DomainException>(() => serviceOrder.AddService(Guid.NewGuid(), -10m));
        }

        [Fact]
        public void RemoveService_ShouldRemoveServiceFromServiceOrder()
        {
            var serviceOrder = CreateServiceOrder();
            var serviceId = Guid.NewGuid();
            serviceOrder.AddService(serviceId, 100m);

            serviceOrder.RemoveService(serviceId);

            Assert.Empty(serviceOrder.Services);
        }

        [Fact]
        public void RemoveService_WhenNotFound_ShouldThrowException()
        {
            var serviceOrder = CreateServiceOrder();

            Assert.Throws<DomainException>(() => serviceOrder.RemoveService(Guid.NewGuid()));
        }

        [Fact]
        public void CalculateBudget_ShouldSumPartsAndServices()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.AddPart(Guid.NewGuid(), 2, 50m);
            serviceOrder.AddService(Guid.NewGuid(), 150m);

            var total = serviceOrder.CalculateBudget();

            Assert.Equal(250m, total);
        }

        [Fact]
        public void CreateBudget_ShouldSetCalculatedBudget()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.AddPart(Guid.NewGuid(), 1, 80m);
            serviceOrder.AddService(Guid.NewGuid(), 120m);

            serviceOrder.CreateBudget();

            Assert.Equal(200m, serviceOrder.Budget);
        }

        [Fact]
        public void FinishAnalysis_WithoutExplicitBudget_ShouldUseCalculatedBudget()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.AddPart(Guid.NewGuid(), 2, 100m);
            serviceOrder.AddService(Guid.NewGuid(), 200m);

            serviceOrder.FinishAnalysis();

            Assert.Equal(400m, serviceOrder.Budget);
            Assert.Equal(OrderStatus.AwaitingApproval, serviceOrder.GetCurrentStatus());
        }

        [Fact]
        public void ReceivePayment_WhenAwaitingApproval_ShouldChangeStatusToInProgressAndMarkPaid()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(300m);

            serviceOrder.ReceivePayment(300m, "TX-12345");

            Assert.True(serviceOrder.IsPaid);
            Assert.NotNull(serviceOrder.PaidAt);
            Assert.Equal(300m, serviceOrder.PaidAmount);
            Assert.Equal("TX-12345", serviceOrder.PaymentTransactionId);
            Assert.Equal(OrderStatus.InProgress, serviceOrder.GetCurrentStatus());
        }

        [Fact]
        public void ReceivePayment_WhenAlreadyPaid_ShouldThrowException()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(300m);
            serviceOrder.ReceivePayment(300m);

            Assert.Throws<DomainException>(() => serviceOrder.ReceivePayment(300m));
        }

        [Fact]
        public void ReceivePayment_BeforeAnalysis_ShouldThrowException()
        {
            var serviceOrder = CreateServiceOrder();

            Assert.Throws<DomainException>(() => serviceOrder.ReceivePayment(100m));
        }

        [Fact]
        public void ReceivePayment_WhenRejected_ShouldThrowException()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(300m);
            serviceOrder.Reject();

            Assert.Throws<DomainException>(() => serviceOrder.ReceivePayment(300m));
        }

        [Fact]
        public void FinishExecution_WithUnensuredParts_ShouldThrowException()
        {
            var serviceOrder = CreateServiceOrder();
            var partId = Guid.NewGuid();
            serviceOrder.AddPart(partId, 1, 50m, "Brake Pad", stockQuantityWasEnsured: false);
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(100m);
            serviceOrder.ReceivePayment(100m, "TX-DIRECT");

            Assert.Equal(OrderStatus.InProgress, serviceOrder.GetCurrentStatus());
            Assert.Throws<DomainException>(() => serviceOrder.FinishExecution(false));

            serviceOrder.EnsurePartStock(partId);
            serviceOrder.FinishExecution(false);

            Assert.Equal(OrderStatus.Completed, serviceOrder.GetCurrentStatus());
        }

        [Fact]
        public void Reject_WhenWaitingPayment_ShouldChangeStatusToRejectedAndClearPaymentUrl()
        {
            var serviceOrder = CreateServiceOrder();
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(350m);
            serviceOrder.Approve();
            serviceOrder.RegisterPaymentCreated("https://mp/checkout", "PREF-1");

            serviceOrder.Reject();

            Assert.Equal(OrderStatus.Rejected, serviceOrder.GetCurrentStatus());
            Assert.Null(serviceOrder.PaymentUrl);
        }

        [Fact]
        public void ConfirmPayment_WithParts_ShouldWaitForAllPartStocksBeforeInProgress()
        {
            var serviceOrder = CreateServiceOrder();
            var part1 = Guid.NewGuid();
            var part2 = Guid.NewGuid();
            serviceOrder.AddPart(part1, 1, 100m, "Part 1");
            serviceOrder.AddPart(part2, 2, 50m, "Part 2");
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis();
            serviceOrder.Approve();
            serviceOrder.RegisterPaymentCreated("https://mp/checkout", "PREF-99");

            serviceOrder.ConfirmPayment("PREF-99");

            Assert.True(serviceOrder.IsPaid);
            Assert.Equal(OrderStatus.WaitingPayment, serviceOrder.GetCurrentStatus());

            serviceOrder.ConfirmPartStockEnsured(part1);
            Assert.Equal(OrderStatus.WaitingPayment, serviceOrder.GetCurrentStatus());

            serviceOrder.ConfirmPartStockEnsured(part2);
            Assert.Equal(OrderStatus.InProgress, serviceOrder.GetCurrentStatus());
        }

        [Fact]
        public void RevertToAwaitingApproval_ShouldClearPaymentAndResetParts()
        {
            var serviceOrder = CreateServiceOrder();
            var partId = Guid.NewGuid();
            serviceOrder.AddPart(partId, 1, 100m, "Clutch");
            serviceOrder.StartDiagnostics();
            serviceOrder.FinishAnalysis(250m);
            serviceOrder.Approve();
            serviceOrder.RegisterPaymentCreated("https://mp/checkout", "PREF-77");
            serviceOrder.ConfirmPayment("PREF-77");

            serviceOrder.RevertToAwaitingApproval();

            Assert.Equal(OrderStatus.AwaitingApproval, serviceOrder.GetCurrentStatus());
            Assert.False(serviceOrder.IsPaid);
            Assert.Null(serviceOrder.PaidAt);
            Assert.Null(serviceOrder.PaidAmount);
            Assert.Null(serviceOrder.PaymentUrl);
            Assert.Null(serviceOrder.PaymentTransactionId);
            Assert.All(serviceOrder.Parts, p => Assert.False(p.StockQuantityWasEnsured));
        }

        [Fact]
        public void ProcessedSagaEvent_WithInvalidParameters_ShouldThrowDomainException()
        {
            Assert.Throws<DomainException>(() => new ProcessedSagaEvent(Guid.Empty, "PaymentConfirmed", Guid.NewGuid()));
            Assert.Throws<DomainException>(() => new ProcessedSagaEvent(Guid.NewGuid(), "", Guid.NewGuid()));
        }

        [Fact]
        public void OutboxMessage_ValidationAndRecordFailure_ShouldEnforceInvariantsAndTruncateErrors()
        {
            Assert.Throws<DomainException>(() => new OutboxMessage(Guid.Empty, "BudgetApproved", Guid.NewGuid(), "{}"));
            Assert.Throws<DomainException>(() => new OutboxMessage(Guid.NewGuid(), "", Guid.NewGuid(), "{}"));
            Assert.Throws<DomainException>(() => new OutboxMessage(Guid.NewGuid(), "BudgetApproved", Guid.NewGuid(), ""));

            var message = new OutboxMessage(
                Guid.NewGuid(),
                "BudgetApproved",
                Guid.NewGuid(),
                "{\"eventId\":\"123\"}",
                "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

            Assert.Equal(0, message.Attempts);
            Assert.Null(message.LastError);

            var oversizedError = new string('E', OutboxMessage.MaxErrorLength + 250);
            message.RecordFailure(oversizedError);

            Assert.Equal(1, message.Attempts);
            Assert.NotNull(message.LastError);
            Assert.Equal(OutboxMessage.MaxErrorLength, message.LastError.Length);

            message.RecordFailure("SNS timeout");
            Assert.Equal(2, message.Attempts);
            Assert.Equal("SNS timeout", message.LastError);
        }
    }
}
