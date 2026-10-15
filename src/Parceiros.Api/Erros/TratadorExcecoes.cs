using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Parceiros.Dominio;

namespace Parceiros.Api.Erros;

/// <summary>
/// Converte exceções em respostas de erro com formato único (ProblemDetails, RFC 7807),
/// sem expor stack traces.
/// </summary>
public class TratadorExcecoes(ILogger<TratadorExcecoes> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excecao, CancellationToken ct)
    {
        var problema = excecao switch
        {
            ExcecaoValidacao v => Criar(400, "Pedido inválido", v.Message, v.Erros),
            ExcecaoNaoEncontrado n => Criar(404, "Não encontrado", n.Message),
            ExcecaoConflito c => Criar(409, "Conflito", c.Message),
            BadHttpRequestException b => Criar(400, "Pedido inválido", "O corpo do pedido é inválido ou está malformado."),
            _ => null
        };

        if (problema is null)
        {
            logger.LogError(excecao, "Erro inesperado no pedido {Metodo} {Caminho}", contexto.Request.Method, contexto.Request.Path);
            problema = Criar(500, "Erro interno", "Ocorreu um erro inesperado. Tente novamente mais tarde.");
        }

        contexto.Response.StatusCode = problema.Status!.Value;
        await contexto.Response.WriteAsJsonAsync(problema, options: null, contentType: "application/problem+json", ct);
        return true;
    }

    public static ProblemDetails Criar(int estado, string titulo, string detalhe, IReadOnlyDictionary<string, string[]>? erros = null)
    {
        var p = new ProblemDetails { Status = estado, Title = titulo, Detail = detalhe };
        if (erros is not null) p.Extensions["errors"] = erros;
        return p;
    }
}
