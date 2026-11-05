using OSService.Application.Comandos;
using OSService.Domain.Entidades;
using OSService.Domain.Interfaces;

namespace OSService.Application.Servicos
{
    public class OrdemServicoServico
    {
        private readonly IOrdemServicoRepositorio _repositorio;

        public OrdemServicoServico(IOrdemServicoRepositorio repositorio)
        {
            _repositorio = repositorio;
        }

        public async Task<OrdemServicoConsultaComandoSaida> IncluirAsync(
            OrdemServicoCadastroComandoEntrada comando)
        {
            ArgumentNullException.ThrowIfNull(comando);

            var ordemServico = new OrdemServico(
                comando.ClienteId,
                comando.VeiculoId);

            await _repositorio.IncluirAsync(ordemServico);
            await _repositorio.SalvarAlteracoesAsync();

            return MapearSaida(ordemServico);
        }

        public async Task<OrdemServicoConsultaComandoSaida?> LocalizarPorIdAsync(Guid id)
        {
            var ordemServico = await _repositorio.LocalizarPorIdAsync(id);

            if (ordemServico == null)
                return null;

            return MapearSaida(ordemServico);
        }

        public async Task<List<OrdemServicoConsultaComandoSaida>> ListarAsync()
        {
            var ordensServico = await _repositorio.ListarAsync();

            return ordensServico
                .Select(MapearSaida)
                .ToList();
        }

        private static OrdemServicoConsultaComandoSaida MapearSaida(
            OrdemServico ordemServico)
        {
            return new OrdemServicoConsultaComandoSaida
            {
                Id = ordemServico.Id,
                ClienteId = ordemServico.ClienteId,
                VeiculoId = ordemServico.VeiculoId,
                DataCriacao = ordemServico.DataCriacao,
                Orcamento = ordemServico.Orcamento,
                Inativo = ordemServico.Inativo,
                Status = ordemServico.ObterStatusAtual()
            };
        }

        #region Alteracao status da o.s
        public async Task<OrdemServicoConsultaComandoSaida> IniciarDiagnosticoAsync(Guid id)
        {
            var ordemServico = await LocalizarEntidadeAsync(id);

            ordemServico.IniciarDiagnostico();

            await _repositorio.SalvarAlteracoesAsync();

            return MapearSaida(ordemServico);
        }

        public async Task<OrdemServicoConsultaComandoSaida> FinalizarAnaliseAsync(
            Guid id,
            OrdemServicoFinalizarAnaliseComandoEntrada comando)
        {
            ArgumentNullException.ThrowIfNull(comando);

            var ordemServico = await LocalizarEntidadeAsync(id);

            ordemServico.FinalizarAnalise(comando.Orcamento);

            await _repositorio.SalvarAlteracoesAsync();

            return MapearSaida(ordemServico);
        }

        public async Task<OrdemServicoConsultaComandoSaida> AprovarAsync(Guid id)
        {
            var ordemServico = await LocalizarEntidadeAsync(id);

            ordemServico.Aprovar();

            await _repositorio.SalvarAlteracoesAsync();

            return MapearSaida(ordemServico);
        }

        public async Task<OrdemServicoConsultaComandoSaida> RecusarAsync(Guid id)
        {
            var ordemServico = await LocalizarEntidadeAsync(id);

            ordemServico.Recusar();

            await _repositorio.SalvarAlteracoesAsync();

            return MapearSaida(ordemServico);
        }

        public async Task<OrdemServicoConsultaComandoSaida> EntregarAsync(Guid id)
        {
            var ordemServico = await LocalizarEntidadeAsync(id);

            ordemServico.Entregar();

            await _repositorio.SalvarAlteracoesAsync();

            return MapearSaida(ordemServico);
        }

        private async Task<OrdemServico> LocalizarEntidadeAsync(Guid id)
        {
            return await _repositorio.LocalizarPorIdAsync(id)
                ?? throw new KeyNotFoundException(
                    $"Ordem de serviço {id} não encontrada.");
        }

        #endregion
    }
}
