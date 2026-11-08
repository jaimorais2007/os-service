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

            builder.Property(x => x.IsPaid)
                .IsRequired();

            builder.Property(x => x.PaidAt)
                .IsRequired(false);

            builder.Property(x => x.PaidAmount)
                .HasPrecision(18, 2)
                .IsRequired(false);

            builder.Property(x => x.PaymentTransactionId)
                .HasMaxLength(100)
                .IsRequired(false);

            builder.Property(x => x.PaymentUrl)
                .HasMaxLength(500)
                .IsRequired(false);

            builder.HasMany(x => x.StatusHistory)
                .WithOne(x => x.ServiceOrder)
                .HasForeignKey(x => x.ServiceOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Navigation(x => x.StatusHistory)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(x => x.Parts)
                .WithOne(x => x.ServiceOrder)
                .HasForeignKey(x => x.ServiceOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(x => x.Parts)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(x => x.Services)
                .WithOne(x => x.ServiceOrder)
                .HasForeignKey(x => x.ServiceOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(x => x.Services)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
