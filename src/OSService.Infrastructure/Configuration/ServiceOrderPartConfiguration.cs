using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OSService.Domain.Entities;

namespace OSService.Infrastructure.Mappings
{
    public class ServiceOrderPartConfiguration : IEntityTypeConfiguration<ServiceOrderPart>
    {
        public void Configure(EntityTypeBuilder<ServiceOrderPart> builder)
        {
            builder.ToTable("ServiceOrderPart");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .ValueGeneratedNever();

            builder.Property(x => x.ServiceOrderId)
                .IsRequired();

            builder.Property(x => x.PartId)
                .IsRequired();

            builder.Property(x => x.PartName)
                .HasMaxLength(200)
                .IsRequired(false);

            builder.Property(x => x.Quantity)
                .IsRequired();

            builder.Property(x => x.UnitPrice)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.StockQuantityWasEnsured)
                .IsRequired();

            builder.Ignore(x => x.TotalPrice);

            builder.HasIndex(x => new
            {
                x.ServiceOrderId,
                x.PartId
            });
        }
    }
}
