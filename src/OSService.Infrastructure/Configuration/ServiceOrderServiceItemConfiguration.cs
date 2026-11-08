using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OSService.Domain.Entities;

namespace OSService.Infrastructure.Mappings
{
    public class ServiceOrderServiceItemConfiguration : IEntityTypeConfiguration<ServiceOrderServiceItem>
    {
        public void Configure(EntityTypeBuilder<ServiceOrderServiceItem> builder)
        {
            builder.ToTable("ServiceOrderService");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .ValueGeneratedNever();

            builder.Property(x => x.ServiceOrderId)
                .IsRequired();

            builder.Property(x => x.ServiceId)
                .IsRequired();

            builder.Property(x => x.ServiceName)
                .HasMaxLength(200)
                .IsRequired(false);

            builder.Property(x => x.Price)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.HasIndex(x => new
            {
                x.ServiceOrderId,
                x.ServiceId
            });
        }
    }
}
