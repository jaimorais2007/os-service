using Microsoft.EntityFrameworkCore;
using OSService.Domain.Entities;

namespace OSService.Infrastructure.Contexts
{
    public class ServiceOrderDbContext : DbContext
    {
        public ServiceOrderDbContext(
            DbContextOptions<ServiceOrderDbContext> options)
            : base(options)
        {
        }

        public DbSet<ServiceOrder> ServiceOrders { get; set; }

        public DbSet<ServiceOrderStatus> ServiceOrderStatuses { get; set; }

        public DbSet<ServiceOrderPart> ServiceOrderParts { get; set; }

        public DbSet<ServiceOrderServiceItem> ServiceOrderServices { get; set; }

        public DbSet<ProcessedSagaEvent> ProcessedSagaEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(ServiceOrderDbContext).Assembly);

            base.OnModelCreating(modelBuilder);
        }
    }
}
