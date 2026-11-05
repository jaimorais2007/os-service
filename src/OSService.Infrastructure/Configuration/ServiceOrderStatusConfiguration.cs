
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OSService.Domain.Entities;

namespace OSService.Infrastructure.Mappings
{
    public class ServiceOrderStatusConfiguration
        : IEntityTypeConfiguration<ServiceOrderStatus>
    {
        public void Configure(EntityTypeBuilder<ServiceOrderStatus> builder)
        {
            builder.ToTable("ServiceOrderStatus");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .ValueGeneratedNever();

            builder.Property(x => x.ServiceOrderId)
                .IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.HasIndex(x => new
            {
                x.ServiceOrderId,
                x.CreatedAt
            });
        }
    }
}
