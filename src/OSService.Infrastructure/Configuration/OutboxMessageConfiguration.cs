using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OSService.Domain.Entities;

namespace OSService.Infrastructure.Mappings
{
    public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            builder.ToTable("OutboxMessage");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .ValueGeneratedNever();

            builder.Property(x => x.EventType)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.CorrelationId)
                .IsRequired();

            builder.Property(x => x.Content)
                .IsRequired();

            builder.Property(x => x.TraceParent)
                .HasMaxLength(200)
                .IsRequired(false);

            builder.Property(x => x.OccurredAt)
                .IsRequired();

            builder.Property(x => x.Attempts)
                .IsRequired();

            builder.Property(x => x.LastError)
                .HasMaxLength(OutboxMessage.MaxErrorLength)
                .IsRequired(false);

            builder.HasIndex(x => x.OccurredAt);
        }
    }
}
