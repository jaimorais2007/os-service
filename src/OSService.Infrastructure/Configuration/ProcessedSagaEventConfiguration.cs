using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OSService.Domain.Entities;

namespace OSService.Infrastructure.Mappings
{
    public class ProcessedSagaEventConfiguration : IEntityTypeConfiguration<ProcessedSagaEvent>
    {
        public void Configure(EntityTypeBuilder<ProcessedSagaEvent> builder)
        {
            builder.ToTable("ProcessedSagaEvent");

            builder.HasKey(x => x.EventId);

            builder.Property(x => x.EventId)
                .ValueGeneratedNever();

            builder.Property(x => x.EventType)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.CorrelationId)
                .IsRequired();

            builder.Property(x => x.ProcessedAt)
                .IsRequired();
        }
    }
}
