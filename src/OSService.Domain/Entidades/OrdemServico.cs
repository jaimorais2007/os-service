using OSService.Domain.Enum;
using OSService.Domain.Excecoes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OSService.Domain.Entidades
{
    public class OrdemServico
    {
        public Guid Id { get; private set; }
        public Guid ClienteId { get; private set; }
        public Guid VeiculoId { get; private set; }
        public DateTime DataCriacao { get; private set; }
        public decimal Orcamento { get; private set; }
        public bool Inativo { get; private set; }

        public ICollection<HistoricoStatusOrdemServico> HistoricoStatus
        { get; private set; } = new List<HistoricoStatusOrdemServico>();

        protected OrdemServico()
        {
        }

        public OrdemServico(Guid clienteId, Guid veiculoId)
        {
            if (clienteId == Guid.Empty)
                throw new DominioExcecao("O cliente deve ser informado.");

            if (veiculoId == Guid.Empty)
                throw new DominioExcecao("O veículo deve ser informado.");

            Id = Guid.NewGuid();
            ClienteId = clienteId;
            VeiculoId = veiculoId;
            DataCriacao = DateTime.UtcNow;
            Inativo = false;

            AlterarStatus(StatusOrdemServicoEnum.Recebida);
        }

        public StatusOrdemServicoEnum ObterStatusAtual()
        {
            return ObterUltimoHistoricoStatus().Status;
        }

        public HistoricoStatusOrdemServico ObterUltimoHistoricoStatus()
        {
            return HistoricoStatus
                .OrderByDescending(x => x.DataCriacao)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault()
                ?? throw new DominioExcecao(
                    $"A ordem de serviço {Id} não possui histórico de status.");
        }

        public void IniciarDiagnostico()
        {
            ValidarStatus(
                StatusOrdemServicoEnum.Recebida,
                "A ordem de serviço deve estar no status 'Recebida' para iniciar a análise técnica.");

            AlterarStatus(StatusOrdemServicoEnum.EmAnalise);
        }

        public void FinalizarAnalise(decimal orcamento)
        {
            ValidarStatus(
                StatusOrdemServicoEnum.EmAnalise,
                "A ordem de serviço deve estar no status 'Em Análise' para finalizar a análise técnica.");

            if (orcamento < 0)
                throw new DominioExcecao("O orçamento não pode ser negativo.");

            Orcamento = orcamento;

            AlterarStatus(StatusOrdemServicoEnum.AguardandoAprovacao);
        }

        public void Aprovar()
        {
            ValidarStatus(
                StatusOrdemServicoEnum.AguardandoAprovacao,
                "A ordem de serviço deve estar no status 'Aguardando Aprovação' para ser aprovada.");

            AlterarStatus(StatusOrdemServicoEnum.EmExecucao);
        }

        public void FinalizarExecucao(bool possuiPecasPendentes)
        {
            ValidarStatus(
                StatusOrdemServicoEnum.EmExecucao,
                "A ordem de serviço deve estar no status 'Em Execução' para finalizar a execução.");

            if (possuiPecasPendentes)
                throw new DominioExcecao(
                    "Não é possível finalizar a execução de uma ordem de serviço que possui peças pendentes.");

            AlterarStatus(StatusOrdemServicoEnum.Finalizada);
        }

        public void Entregar()
        {
            ValidarStatus(
                StatusOrdemServicoEnum.Finalizada,
                "A ordem de serviço deve estar no status 'Finalizada' para ser entregue.");

            AlterarStatus(StatusOrdemServicoEnum.Entregue);
        }

        public void Recusar()
        {
            ValidarStatus(
                StatusOrdemServicoEnum.AguardandoAprovacao,
                "A ordem de serviço deve estar no status 'Aguardando Aprovação' para ser recusada.");

            AlterarStatus(StatusOrdemServicoEnum.Recusada);
        }

        private void ValidarStatus(
            StatusOrdemServicoEnum statusEsperado,
            string mensagem)
        {
            if (ObterStatusAtual() != statusEsperado)
                throw new DominioExcecao(mensagem);
        }

        private void AlterarStatus(StatusOrdemServicoEnum status)
        {
            HistoricoStatus.Add(
                new HistoricoStatusOrdemServico(Id, status));
        }
    }
}
