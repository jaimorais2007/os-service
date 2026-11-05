using OSService.Domain.Enum;
namespace OSService.Application.Comandos
{
    public class OrdemServicoConsultaComandoSaida
    {
        public Guid Id { get; set; }

        public Guid ClienteId { get; set; }

        public Guid VeiculoId { get; set; }

        public DateTime DataCriacao { get; set; }

        public decimal Orcamento { get; set; }

        public bool Inativo { get; set; }

        public StatusOrdemServicoEnum Status { get; set; }
    }
}
