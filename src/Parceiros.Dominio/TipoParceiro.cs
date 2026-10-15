namespace Parceiros.Dominio;

/// <summary>Papel da entidade na empresa. Valores expostos na API: CUSTOMER, SUPPLIER, BOTH.</summary>
public enum TipoParceiro
{
    Cliente = 1,
    Fornecedor = 2,
    Ambos = 3
}

public static class TipoParceiroExtensoes
{
    public static string ParaApi(this TipoParceiro tipo) => tipo switch
    {
        TipoParceiro.Cliente => "CUSTOMER",
        TipoParceiro.Fornecedor => "SUPPLIER",
        TipoParceiro.Ambos => "BOTH",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo))
    };

    public static bool TentarLerDaApi(string? texto, out TipoParceiro tipo)
    {
        switch (texto?.Trim().ToUpperInvariant())
        {
            case "CUSTOMER": tipo = TipoParceiro.Cliente; return true;
            case "SUPPLIER": tipo = TipoParceiro.Fornecedor; return true;
            case "BOTH": tipo = TipoParceiro.Ambos; return true;
            default: tipo = default; return false;
        }
    }
}
