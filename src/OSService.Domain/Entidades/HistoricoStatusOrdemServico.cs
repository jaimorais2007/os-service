
using OSService.Domain.Enum;
using System;

namespace OSService.Domain.Entidades
{
    public class HistoricoStatusOrdemServico
    {
        public Guid Id { get; private set; }
        public Guid OrdemServicoId { get; private set; }
        public StatusOrdemServicoEnum Status { get; private set; }
        public DateTime DataCriacao { get; private set; }

        public OrdemServico OrdemServico { get; private set; } = null!;

        protected HistoricoStatusOrdemServico()
        {
        }

        public HistoricoStatusOrdemServico(
            Guid ordemServicoId,
            StatusOrdemServicoEnum status)
        {
            Id = Guid.NewGuid();
            OrdemServicoId = ordemServicoId;
            Status = status;
            DataCriacao = DateTime.UtcNow;
        }
    }
}
