
using System;

namespace OSService.Domain.Excecoes
{
    public class DominioExcecao : Exception
    {
        public DominioExcecao(string mensagem)
            : base(mensagem)
        {
        }
    }
}
