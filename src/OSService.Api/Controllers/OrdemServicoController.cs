
using Microsoft.AspNetCore.Mvc;
using OSService.Application.Comandos;
using OSService.Application.Servicos;
using OSService.Domain.Excecoes;

namespace OSService.Api.Controllers
{
    [ApiController]
    [Route("api/ordens-servico")]
    public class OrdemServicoController : ControllerBase
    {
        private readonly OrdemServicoServico _ordemServicoServico;

        public OrdemServicoController(
            OrdemServicoServico ordemServicoServico)
        {
            _ordemServicoServico = ordemServicoServico;
        }

        [HttpPost]
        [ProducesResponseType(
            typeof(OrdemServicoConsultaComandoSaida),
            StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Incluir(
            [FromBody] OrdemServicoCadastroComandoEntrada comando)
        {
            try
            {
                var resultado = await _ordemServicoServico.IncluirAsync(comando);

                return CreatedAtAction(
                    nameof(LocalizarPorId),
                    new { id = resultado.Id },
                    resultado);
            }
            catch (DominioExcecao ex)
            {
                return BadRequest(new { mensagem = ex.Message });
            }
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(
            typeof(OrdemServicoConsultaComandoSaida),
            StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> LocalizarPorId(Guid id)
        {
            var resultado = await _ordemServicoServico.LocalizarPorIdAsync(id);

            if (resultado == null)
                return NotFound(new
                {
                    mensagem = "Ordem de serviço não encontrada."
                });

            return Ok(resultado);
        }

        [HttpGet]
        [ProducesResponseType(
            typeof(List<OrdemServicoConsultaComandoSaida>),
            StatusCodes.Status200OK)]
        public async Task<IActionResult> Listar()
        {
            var resultado = await _ordemServicoServico.ListarAsync();

            return Ok(resultado);
        }


        [HttpPost("{id:guid}/iniciar-diagnostico")]
        public async Task<IActionResult> IniciarDiagnostico(Guid id)
        {
            return await ExecutarAlteracaoStatusAsync(
                () => _ordemServicoServico.IniciarDiagnosticoAsync(id));
        }

        [HttpPost("{id:guid}/finalizar-analise")]
        public async Task<IActionResult> FinalizarAnalise(
            Guid id,
            [FromBody] OrdemServicoFinalizarAnaliseComandoEntrada comando)
        {
            return await ExecutarAlteracaoStatusAsync(
                () => _ordemServicoServico.FinalizarAnaliseAsync(id, comando));
        }

        [HttpPost("{id:guid}/aprovar")]
        public async Task<IActionResult> Aprovar(Guid id)
        {
            return await ExecutarAlteracaoStatusAsync(
                () => _ordemServicoServico.AprovarAsync(id));
        }

        [HttpPost("{id:guid}/recusar")]
        public async Task<IActionResult> Recusar(Guid id)
        {
            return await ExecutarAlteracaoStatusAsync(
                () => _ordemServicoServico.RecusarAsync(id));
        }

        [HttpPost("{id:guid}/entregar")]
        public async Task<IActionResult> Entregar(Guid id)
        {
            return await ExecutarAlteracaoStatusAsync(
                () => _ordemServicoServico.EntregarAsync(id));
        }

        private async Task<IActionResult> ExecutarAlteracaoStatusAsync(
            Func<Task<OrdemServicoConsultaComandoSaida>> operacao)
        {
            try
            {
                var resultado = await operacao();
                return Ok(resultado);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mensagem = ex.Message });
            }
            catch (DominioExcecao ex)
            {
                return BadRequest(new { mensagem = ex.Message });
            }
        }

    }
}
