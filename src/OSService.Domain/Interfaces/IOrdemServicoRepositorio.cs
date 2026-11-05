using OSService.Domain.Entidades;

namespace OSService.Domain.Interfaces
{
    public interface IOrdemServicoRepositorio
    {
        Task IncluirAsync(OrdemServico ordemServico);

        Task<OrdemServico?> LocalizarPorIdAsync(Guid id);

        Task<List<OrdemServico>> ListarAsync();

        Task SalvarAlteracoesAsync();
    }
}
