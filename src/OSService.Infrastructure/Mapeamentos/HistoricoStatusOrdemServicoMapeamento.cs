
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OSService.Domain.Entidades;

namespace OSService.Infrastructure.Mapeamentos
{
    public class HistoricoStatusOrdemServicoMapeamento
        : IEntityTypeConfiguration<HistoricoStatusOrdemServico>
    {
        public void Configure(EntityTypeBuilder<HistoricoStatusOrdemServico> builder)
        {
            builder.ToTable("HistoricoStatusOrdemServico");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .ValueGeneratedNever();

            builder.Property(x => x.OrdemServicoId)
                .IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(x => x.DataCriacao)
                .IsRequired();

            builder.HasIndex(x => new
            {
                x.OrdemServicoId,
                x.DataCriacao
            });
        }
    }
}
