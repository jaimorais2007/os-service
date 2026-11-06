
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OSService.Domain.Entities;

namespace OSService.Infrastructure.Mappings
{
    public class ServiceOrderConfiguration : IEntityTypeConfiguration<ServiceOrder>
    {
        public void Configure(EntityTypeBuilder<ServiceOrder> builder)
        {
            builder.ToTable("ServiceOrder");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .ValueGeneratedNever();

            builder.Property(x => x.CustomerId)
                .IsRequired();

            builder.Property(x => x.VehicleId)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.Budget)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.Inactive)
                .IsRequired();

            builder.HasMany(x => x.StatusHistory)
                .WithOne(x => x.ServiceOrder)
                .HasForeignKey(x => x.ServiceOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Navigation(x => x.StatusHistory)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
