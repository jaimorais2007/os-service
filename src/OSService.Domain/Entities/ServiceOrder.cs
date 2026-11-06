
using OSService.Domain.Enum;
using OSService.Domain.Exceptions;

namespace OSService.Domain.Entities
{
    public class ServiceOrder
    {
        public Guid Id { get; private set; }

        public Guid CustomerId { get; private set; }

        public Guid VehicleId { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public decimal Budget { get; private set; }

        public bool Inactive { get; private set; }

        public ICollection<ServiceOrderStatus> StatusHistory
        { get; private set; } = new List<ServiceOrderStatus>();

        protected ServiceOrder()
        {
        }

        public ServiceOrder(Guid customerId, Guid vehicleId)
        {
            if (customerId == Guid.Empty)
                throw new DomainException("Customer must be provided.");

            if (vehicleId == Guid.Empty)
                throw new DomainException("Vehicle must be provided.");

            Id = Guid.NewGuid();
            CustomerId = customerId;
            VehicleId = vehicleId;
            CreatedAt = DateTime.UtcNow;
            Inactive = false;

            ChangeStatus(OrderStatus.Received);
        }

        public OrderStatus GetCurrentStatus()
        {
            return GetLatestStatusHistory().Status;
        }

        public ServiceOrderStatus GetLatestStatusHistory()
        {
            return StatusHistory
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault()
                ?? throw new DomainException(
                    $"Service order {Id} has no status history.");
        }

        public void StartDiagnostics()
        {
            ValidateStatus(
                OrderStatus.Received,
                "Service order must be in 'Received' status to start diagnostics.");

            ChangeStatus(OrderStatus.UnderAnalysis);
        }

        public void FinishAnalysis(decimal budget)
        {
            ValidateStatus(
                OrderStatus.UnderAnalysis,
                "Service order must be in 'Under Analysis' status to finish analysis.");

            if (budget < 0)
                throw new DomainException("Budget cannot be negative.");

            Budget = budget;

            ChangeStatus(OrderStatus.AwaitingApproval);
        }

        public void Approve()
        {
            ValidateStatus(
                OrderStatus.AwaitingApproval,
                "Service order must be in 'Awaiting Approval' status to be approved.");

            ChangeStatus(OrderStatus.InProgress);
        }

        public void FinishExecution(bool hasPendingParts)
        {
            ValidateStatus(
                OrderStatus.InProgress,
                "Service order must be in 'In Progress' status to finish execution.");

            if (hasPendingParts)
                throw new DomainException(
                    "Cannot finish a service order with pending parts.");

            ChangeStatus(OrderStatus.Completed);
        }

        public void Deliver()
        {
            ValidateStatus(
                OrderStatus.Completed,
                "Service order must be in 'Completed' status to be delivered.");

            ChangeStatus(OrderStatus.Delivered);
        }

        public void Reject()
        {
            ValidateStatus(
                OrderStatus.AwaitingApproval,
                "Service order must be in 'Awaiting Approval' status to be rejected.");

            ChangeStatus(OrderStatus.Rejected);
        }

        private void ValidateStatus(
            OrderStatus expectedStatus,
            string message)
        {
            if (GetCurrentStatus() != expectedStatus)
                throw new DomainException(message);
        }

        private void ChangeStatus(OrderStatus status)
        {
            StatusHistory.Add(
                new ServiceOrderStatus(Id, status));
        }
    }
}
