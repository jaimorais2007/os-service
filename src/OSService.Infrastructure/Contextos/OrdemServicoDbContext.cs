
using Microsoft.EntityFrameworkCore;
using OSService.Domain.Entidades;

namespace OSService.Infrastructure.Contextos
{
    public class OrdemServicoDbContext : DbContext
    {
        public OrdemServicoDbContext(
            DbContextOptions<OrdemServicoDbContext> options)
            : base(options)
        {
        }

        public DbSet<OrdemServico> OrdensServico { get; set; }

        public DbSet<HistoricoStatusOrdemServico> HistoricosStatusOrdemServico { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(OrdemServicoDbContext).Assembly);

            base.OnModelCreating(modelBuilder);
        }
    }
}
