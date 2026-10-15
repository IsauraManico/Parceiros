namespace Parceiros.Dominio;

/// <summary>Pedido malformado ou com valores inválidos (HTTP 400).</summary>
public class ExcecaoValidacao : Exception
{
    public IReadOnlyDictionary<string, string[]> Erros { get; }

    public ExcecaoValidacao(IReadOnlyDictionary<string, string[]> erros)
        : base("Um ou mais campos são inválidos.") => Erros = erros;

    public ExcecaoValidacao(string campo, string mensagem)
        : this(new Dictionary<string, string[]> { [campo] = [mensagem] }) { }
}

/// <summary>Id inexistente (HTTP 404).</summary>
public class ExcecaoNaoEncontrado(string mensagem) : Exception(mensagem);

/// <summary>NIF duplicado ou versão desatualizada (HTTP 409).</summary>
public class ExcecaoConflito(string mensagem) : Exception(mensagem);
