
using OSService.Domain.Entidades;
using OSService.Domain.Enum;
using OSService.Domain.Excecoes;
using Xunit;

namespace OSService.Tests.Dominio
{
    public class OrdemServicoTestes
    {
        private static OrdemServico CriarOrdemServico()
        {
            return new OrdemServico(
                Guid.NewGuid(),
                Guid.NewGuid());
        }

        [Fact]
        public void CriarOrdemServico_DeveIniciarComStatusRecebida()
        {
            var ordemServico = CriarOrdemServico();

            Assert.Equal(
                StatusOrdemServicoEnum.Recebida,
                ordemServico.ObterStatusAtual());

            Assert.Single(ordemServico.HistoricoStatus);
            Assert.False(ordemServico.Inativo);
        }

        [Fact]
        public void IniciarDiagnostico_DeveAlterarStatusParaEmAnalise()
        {
            var ordemServico = CriarOrdemServico();

            ordemServico.IniciarDiagnostico();

            Assert.Equal(
                StatusOrdemServicoEnum.EmAnalise,
                ordemServico.ObterStatusAtual());

            Assert.Equal(2, ordemServico.HistoricoStatus.Count);
        }

        [Fact]
        public void FinalizarAnalise_DeveAtualizarOrcamentoEStatus()
        {
            var ordemServico = CriarOrdemServico();
            ordemServico.IniciarDiagnostico();

            ordemServico.FinalizarAnalise(350m);

            Assert.Equal(350m, ordemServico.Orcamento);
            Assert.Equal(
                StatusOrdemServicoEnum.AguardandoAprovacao,
                ordemServico.ObterStatusAtual());
        }

        [Fact]
        public void Aprovar_DeveAlterarStatusParaEmExecucao()
        {
            var ordemServico = CriarOrdemServico();
            ordemServico.IniciarDiagnostico();
            ordemServico.FinalizarAnalise(350m);

            ordemServico.Aprovar();

            Assert.Equal(
                StatusOrdemServicoEnum.EmExecucao,
                ordemServico.ObterStatusAtual());
        }

        [Fact]
        public void Recusar_DeveAlterarStatusParaRecusada()
        {
            var ordemServico = CriarOrdemServico();
            ordemServico.IniciarDiagnostico();
            ordemServico.FinalizarAnalise(350m);

            ordemServico.Recusar();

            Assert.Equal(
                StatusOrdemServicoEnum.Recusada,
                ordemServico.ObterStatusAtual());
        }

        [Fact]
        public void Entregar_SemFinalizarExecucao_DeveLancarExcecao()
        {
            var ordemServico = CriarOrdemServico();

            var excecao = Assert.Throws<DominioExcecao>(
                () => ordemServico.Entregar());

            Assert.Contains(
                "Finalizada",
                excecao.Message);
        }

        [Fact]
        public void FinalizarAnalise_ComOrcamentoNegativo_DeveLancarExcecao()
        {
            var ordemServico = CriarOrdemServico();
            ordemServico.IniciarDiagnostico();

            Assert.Throws<DominioExcecao>(
                () => ordemServico.FinalizarAnalise(-10m));
        }

        [Fact]
        public void CriarOrdemServico_SemCliente_DeveLancarExcecao()
        {
            Assert.Throws<DominioExcecao>(
                () => new OrdemServico(
                    Guid.Empty,
                    Guid.NewGuid()));
        }

        [Fact]
        public void FinalizarExecucao_ComPecasPendentes_DeveLancarExcecao()
        {
            var ordemServico = CriarOrdemServico();
            ordemServico.IniciarDiagnostico();
            ordemServico.FinalizarAnalise(350m);
            ordemServico.Aprovar();

            Assert.Throws<DominioExcecao>(
                () => ordemServico.FinalizarExecucao(true));
        }

        [Fact]
        public void FinalizarExecucao_SemPecasPendentes_DeveFinalizar()
        {
            var ordemServico = CriarOrdemServico();
            ordemServico.IniciarDiagnostico();
            ordemServico.FinalizarAnalise(350m);
            ordemServico.Aprovar();

            ordemServico.FinalizarExecucao(false);

            Assert.Equal(
                StatusOrdemServicoEnum.Finalizada,
                ordemServico.ObterStatusAtual());
        }

        [Fact]
        public void Entregar_AposFinalizarExecucao_DeveAlterarStatusParaEntregue()
        {
            var ordemServico = CriarOrdemServico();
            ordemServico.IniciarDiagnostico();
            ordemServico.FinalizarAnalise(350m);
            ordemServico.Aprovar();
            ordemServico.FinalizarExecucao(false);

            ordemServico.Entregar();

            Assert.Equal(
                StatusOrdemServicoEnum.Entregue,
                ordemServico.ObterStatusAtual());
        }
    }
}
