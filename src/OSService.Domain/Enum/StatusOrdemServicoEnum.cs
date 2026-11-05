using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OSService.Domain.Enum
{
    public enum StatusOrdemServicoEnum
    {
        Recebida = 1,
        EmAnalise = 2,
        AguardandoAprovacao = 3,
        EmExecucao = 4,
        Finalizada = 5,
        Entregue = 6,
        Recusada = 7
    }
}
