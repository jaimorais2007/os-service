
using Microsoft.EntityFrameworkCore;
using OSService.Domain.Entidades;
using OSService.Domain.Interfaces;
using OSService.Infrastructure.Contextos;

namespace OSService.Infrastructure.Repositorios
{
    public class OrdemServicoRepositorio : IOrdemServicoRepositorio
    {
        private readonly OrdemServicoDbContext _contexto;

        public OrdemServicoRepositorio(OrdemServicoDbContext contexto)
        {
            _contexto = contexto;
        }

        public async Task IncluirAsync(OrdemServico ordemServico)
        {
            await _contexto.OrdensServico.AddAsync(ordemServico);
        }

        public async Task<OrdemServico?> LocalizarPorIdAsync(Guid id)
        {
            return await _contexto.OrdensServico
                .Include(x => x.HistoricoStatus)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<List<OrdemServico>> ListarAsync()
        {
            return await _contexto.OrdensServico
                .AsNoTracking()
                .Include(x => x.HistoricoStatus)
                .OrderByDescending(x => x.DataCriacao)
                .ToListAsync();
        }

        public async Task SalvarAlteracoesAsync()
        {
            await _contexto.SaveChangesAsync();
        }
    }
}
