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

        public bool IsPaid { get; private set; }

        public DateTime? PaidAt { get; private set; }

        public decimal? PaidAmount { get; private set; }

        public string? PaymentTransactionId { get; private set; }

        public ICollection<ServiceOrderStatus> StatusHistory { get; private set; } = new List<ServiceOrderStatus>();

        public ICollection<ServiceOrderPart> Parts { get; private set; } = new List<ServiceOrderPart>();

        public ICollection<ServiceOrderServiceItem> Services { get; private set; } = new List<ServiceOrderServiceItem>();

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
            IsPaid = false;

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

        public decimal CalculateBudget()
        {
            var partsTotal = Parts.Sum(p => p.Quantity * p.UnitPrice);
            var servicesTotal = Services.Sum(s => s.Price);
            return partsTotal + servicesTotal;
        }

        public void CreateBudget(decimal? budget = null)
        {
            ValidateStatus(
                OrderStatus.UnderAnalysis,
                "Service order must be in 'Under Analysis' status to create budget.");

            if (budget.HasValue && budget.Value < 0)
                throw new DomainException("Budget cannot be negative.");

            var calculated = CalculateBudget();
            var finalBudget = (budget.HasValue && budget.Value > 0)
                ? budget.Value
                : (calculated > 0 ? calculated : (budget ?? Budget));

            if (finalBudget < 0)
                throw new DomainException("Budget cannot be negative.");

            Budget = finalBudget;
        }

        public void FinishAnalysis(decimal? budget = null)
        {
            ValidateStatus(
                OrderStatus.UnderAnalysis,
                "Service order must be in 'Under Analysis' status to finish analysis.");

            if (budget.HasValue && budget.Value < 0)
                throw new DomainException("Budget cannot be negative.");

            var calculated = CalculateBudget();
            var finalBudget = (budget.HasValue && budget.Value > 0)
                ? budget.Value
                : (calculated > 0 ? calculated : (budget ?? Budget));

            if (finalBudget < 0)
                throw new DomainException("Budget cannot be negative.");

            Budget = finalBudget;

            ChangeStatus(OrderStatus.AwaitingApproval);
        }

        public void Approve()
        {
            ValidateStatus(
                OrderStatus.AwaitingApproval,
                "Service order must be in 'Awaiting Approval' status to be approved.");

            ChangeStatus(OrderStatus.InProgress);
        }

        public void Reject()
        {
            ValidateStatus(
                OrderStatus.AwaitingApproval,
                "Service order must be in 'Awaiting Approval' status to be rejected.");

            ChangeStatus(OrderStatus.Rejected);
        }

        public void ReceivePayment(decimal? amount = null, string? transactionId = null)
        {
            if (Inactive)
                throw new DomainException("Cannot receive payment for an inactive service order.");

            var status = GetCurrentStatus();
            if (status == OrderStatus.Rejected)
                throw new DomainException("Cannot receive payment for a rejected service order.");

            if (status == OrderStatus.Received || status == OrderStatus.UnderAnalysis)
                throw new DomainException("Cannot receive payment before the budget analysis is completed.");

            if (IsPaid)
                throw new DomainException("Payment has already been received for this service order.");

            if (amount.HasValue && amount.Value <= 0)
                throw new DomainException("Payment amount must be greater than zero.");

            IsPaid = true;
            PaidAt = DateTime.UtcNow;
            PaidAmount = amount ?? (Budget > 0 ? Budget : CalculateBudget());
            PaymentTransactionId = transactionId ?? Guid.NewGuid().ToString();

            if (status == OrderStatus.AwaitingApproval)
            {
                ChangeStatus(OrderStatus.InProgress);
            }
        }

        public ServiceOrderPart AddPart(
            Guid partId,
            int quantity,
            decimal unitPrice = 0,
            string? partName = null,
            bool stockQuantityWasEnsured = true)
        {
            ValidateCanModifyItems("Cannot add parts in the current service order status.");

            if (partId == Guid.Empty)
                throw new DomainException("Part must be provided.");

            if (quantity <= 0)
                throw new DomainException("Quantity must be greater than zero.");

            var existing = Parts.FirstOrDefault(p => p.PartId == partId);
            if (existing != null)
            {
                existing.UpdateQuantity(existing.Quantity + quantity);
                if (unitPrice > 0)
                    existing.UpdateUnitPrice(unitPrice);
                return existing;
            }

            var part = new ServiceOrderPart(
                Id,
                partId,
                quantity,
                unitPrice,
                partName,
                stockQuantityWasEnsured);

            Parts.Add(part);
            return part;
        }

        public void RemovePart(Guid partId)
        {
            ValidateCanModifyItems("Cannot remove parts in the current service order status.");

            var part = Parts.FirstOrDefault(p => p.PartId == partId || p.Id == partId)
                ?? throw new DomainException($"Part {partId} was not found in this service order.");

            Parts.Remove(part);
        }

        public void EnsurePartStock(Guid partId)
        {
            var part = Parts.FirstOrDefault(p => p.PartId == partId || p.Id == partId)
                ?? throw new DomainException($"Part {partId} was not found in this service order.");

            part.EnsureStock();
        }

        public ServiceOrderServiceItem AddService(
            Guid serviceId,
            decimal price = 0,
            string? serviceName = null)
        {
            ValidateCanModifyItems("Cannot add services in the current service order status.");

            if (serviceId == Guid.Empty)
                throw new DomainException("Service must be provided.");

            var existing = Services.FirstOrDefault(s => s.ServiceId == serviceId);
            if (existing != null)
                throw new DomainException("Service has already been added to this service order.");

            var service = new ServiceOrderServiceItem(
                Id,
                serviceId,
                price,
                serviceName);

            Services.Add(service);
            return service;
        }

        public void RemoveService(Guid serviceId)
        {
            ValidateCanModifyItems("Cannot remove services in the current service order status.");

            var service = Services.FirstOrDefault(s => s.ServiceId == serviceId || s.Id == serviceId)
                ?? throw new DomainException($"Service {serviceId} was not found in this service order.");

            Services.Remove(service);
        }

        public void FinishExecution(bool hasPendingParts = false)
        {
            ValidateStatus(
                OrderStatus.InProgress,
                "Service order must be in 'In Progress' status to finish execution.");

            if (hasPendingParts || (Parts.Count > 0 && Parts.Any(p => !p.StockQuantityWasEnsured)))
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

        private void ValidateStatus(
            OrderStatus expectedStatus,
            string message)
        {
            if (GetCurrentStatus() != expectedStatus)
                throw new DomainException(message);
        }

        private void ValidateCanModifyItems(string message)
        {
            var currentStatus = GetCurrentStatus();
            if (currentStatus != OrderStatus.Received && currentStatus != OrderStatus.UnderAnalysis)
                throw new DomainException(message);
        }

        private void ChangeStatus(OrderStatus status)
        {
            StatusHistory.Add(
                new ServiceOrderStatus(Id, status));
        }
    }
}
