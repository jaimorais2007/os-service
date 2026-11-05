
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OSService.Domain.Entidades;

namespace OSService.Infrastructure.Mapeamentos
{
    public class OrdemServicoMapeamento : IEntityTypeConfiguration<OrdemServico>
    {
        public void Configure(EntityTypeBuilder<OrdemServico> builder)
        {
            builder.ToTable("OrdemServico");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .ValueGeneratedNever();

            builder.Property(x => x.ClienteId)
                .IsRequired();

            builder.Property(x => x.VeiculoId)
                .IsRequired();

            builder.Property(x => x.DataCriacao)
                .IsRequired();

            builder.Property(x => x.Orcamento)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.Inativo)
                .IsRequired();

            builder.HasMany(x => x.HistoricoStatus)
                .WithOne(x => x.OrdemServico)
                .HasForeignKey(x => x.OrdemServicoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Navigation(x => x.HistoricoStatus)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
