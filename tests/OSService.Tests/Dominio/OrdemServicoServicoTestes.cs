using Moq;
using OSService.Application.Comandos;
using OSService.Application.Servicos;
using OSService.Domain.Entidades;
using OSService.Domain.Enum;
using OSService.Domain.Interfaces;
using Xunit;

namespace OSService.Tests.Aplicacao
{
    public class OrdemServicoServicoTestes
    {
        private readonly Mock<IOrdemServicoRepositorio> _repositorioMock;
        private readonly OrdemServicoServico _servico;

        public OrdemServicoServicoTestes()
        {
            _repositorioMock = new Mock<IOrdemServicoRepositorio>();
            _servico = new OrdemServicoServico(_repositorioMock.Object);
        }

        private static OrdemServico CriarOrdemServico()
        {
            return new OrdemServico(Guid.NewGuid(), Guid.NewGuid());
        }

        [Fact]
        public async Task IncluirAsync_DeveCadastrarOrdemServico()
        {
            var comando = new OrdemServicoCadastroComandoEntrada
            {
                ClienteId = Guid.NewGuid(),
                VeiculoId = Guid.NewGuid()
            };

            OrdemServico? ordemIncluida = null;

            _repositorioMock
                .Setup(x => x.IncluirAsync(It.IsAny<OrdemServico>()))
                .Callback<OrdemServico>(ordem => ordemIncluida = ordem)
                .Returns(Task.CompletedTask);

            var resultado = await _servico.IncluirAsync(comando);

            Assert.NotNull(ordemIncluida);
            Assert.Equal(comando.ClienteId, resultado.ClienteId);
            Assert.Equal(comando.VeiculoId, resultado.VeiculoId);
            Assert.Equal(StatusOrdemServicoEnum.Recebida, resultado.Status);

            _repositorioMock.Verify(
                x => x.IncluirAsync(It.IsAny<OrdemServico>()),
                Times.Once);

            _repositorioMock.Verify(
                x => x.SalvarAlteracoesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task LocalizarPorIdAsync_DeveRetornarOrdemServico()
        {
            var ordemServico = CriarOrdemServico();

            _repositorioMock
                .Setup(x => x.LocalizarPorIdAsync(ordemServico.Id))
                .ReturnsAsync(ordemServico);

            var resultado = await _servico.LocalizarPorIdAsync(ordemServico.Id);

            Assert.NotNull(resultado);
            Assert.Equal(ordemServico.Id, resultado.Id);
            Assert.Equal(StatusOrdemServicoEnum.Recebida, resultado.Status);
        }

        [Fact]
        public async Task LocalizarPorIdAsync_QuandoNaoEncontrar_DeveRetornarNulo()
        {
            var id = Guid.NewGuid();

            _repositorioMock
                .Setup(x => x.LocalizarPorIdAsync(id))
                .ReturnsAsync((OrdemServico?)null);

            var resultado = await _servico.LocalizarPorIdAsync(id);

            Assert.Null(resultado);
        }

        [Fact]
        public async Task ListarAsync_DeveRetornarOrdensServico()
        {
            var ordens = new List<OrdemServico>
            {
                CriarOrdemServico(),
                CriarOrdemServico()
            };

            _repositorioMock
                .Setup(x => x.ListarAsync())
                .ReturnsAsync(ordens);

            var resultado = await _servico.ListarAsync();

            Assert.Equal(2, resultado.Count);
            Assert.All(resultado, x =>
                Assert.Equal(StatusOrdemServicoEnum.Recebida, x.Status));
        }

        [Fact]
        public async Task IniciarDiagnosticoAsync_DeveAlterarStatusESalvar()
        {
            var ordemServico = CriarOrdemServico();

            _repositorioMock
                .Setup(x => x.LocalizarPorIdAsync(ordemServico.Id))
                .ReturnsAsync(ordemServico);

            var resultado = await _servico.IniciarDiagnosticoAsync(ordemServico.Id);

            Assert.Equal(StatusOrdemServicoEnum.EmAnalise, resultado.Status);

            _repositorioMock.Verify(
                x => x.SalvarAlteracoesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task FinalizarAnaliseAsync_DeveAtualizarOrcamentoESalvar()
        {
            var ordemServico = CriarOrdemServico();
            ordemServico.IniciarDiagnostico();

            _repositorioMock
                .Setup(x => x.LocalizarPorIdAsync(ordemServico.Id))
                .ReturnsAsync(ordemServico);

            var comando = new OrdemServicoFinalizarAnaliseComandoEntrada
            {
                Orcamento = 350m
            };

            var resultado = await _servico.FinalizarAnaliseAsync(
                ordemServico.Id, comando);

            Assert.Equal(350m, resultado.Orcamento);
            Assert.Equal(
                StatusOrdemServicoEnum.AguardandoAprovacao,
                resultado.Status);

            _repositorioMock.Verify(
                x => x.SalvarAlteracoesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task AprovarAsync_DeveAlterarStatusESalvar()
        {
            var ordemServico = CriarOrdemServico();
            ordemServico.IniciarDiagnostico();
            ordemServico.FinalizarAnalise(350m);

            _repositorioMock
                .Setup(x => x.LocalizarPorIdAsync(ordemServico.Id))
                .ReturnsAsync(ordemServico);

            var resultado = await _servico.AprovarAsync(ordemServico.Id);

            Assert.Equal(StatusOrdemServicoEnum.EmExecucao, resultado.Status);

            _repositorioMock.Verify(
                x => x.SalvarAlteracoesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task RecusarAsync_DeveAlterarStatusESalvar()
        {
            var ordemServico = CriarOrdemServico();
            ordemServico.IniciarDiagnostico();
            ordemServico.FinalizarAnalise(350m);

            _repositorioMock
                .Setup(x => x.LocalizarPorIdAsync(ordemServico.Id))
                .ReturnsAsync(ordemServico);

            var resultado = await _servico.RecusarAsync(ordemServico.Id);

            Assert.Equal(StatusOrdemServicoEnum.Recusada, resultado.Status);

            _repositorioMock.Verify(
                x => x.SalvarAlteracoesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task EntregarAsync_DeveAlterarStatusESalvar()
        {
            var ordemServico = CriarOrdemServico();
            ordemServico.IniciarDiagnostico();
            ordemServico.FinalizarAnalise(350m);
            ordemServico.Aprovar();
            ordemServico.FinalizarExecucao(false);

            _repositorioMock
                .Setup(x => x.LocalizarPorIdAsync(ordemServico.Id))
                .ReturnsAsync(ordemServico);

            var resultado = await _servico.EntregarAsync(ordemServico.Id);

            Assert.Equal(StatusOrdemServicoEnum.Entregue, resultado.Status);

            _repositorioMock.Verify(
                x => x.SalvarAlteracoesAsync(),
                Times.Once);
        }

        [Fact]
        public async Task IniciarDiagnosticoAsync_QuandoNaoEncontrar_DeveLancarExcecao()
        {
            var id = Guid.NewGuid();

            _repositorioMock
                .Setup(x => x.LocalizarPorIdAsync(id))
                .ReturnsAsync((OrdemServico?)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _servico.IniciarDiagnosticoAsync(id));

            _repositorioMock.Verify(
                x => x.SalvarAlteracoesAsync(),
                Times.Never);
        }
    }
}
