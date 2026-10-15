using System.Text.Json.Serialization;

namespace Parceiros.Api.Modelos;

/// <summary>Corpo do POST /api/partners. Os nomes JSON seguem o enunciado.</summary>
public class CriarParceiroPedido
{
    [JsonPropertyName("Name")] public string? Nome { get; set; }
    [JsonPropertyName("TaxNumber")] public string? Nif { get; set; }
    [JsonPropertyName("Type")] public string? Tipo { get; set; }
    [JsonPropertyName("Email")] public string? Email { get; set; }
    [JsonPropertyName("CreditLimit")] public decimal? LimiteCredito { get; set; }
}

/// <summary>Corpo do PUT /api/partners/{id}. Todos os campos são obrigatórios; Email pode ser null mas tem de vir no corpo.</summary>
public class AtualizarParceiroPedido
{
    private string? _email;

    [JsonPropertyName("Name")] public string? Nome { get; set; }
    [JsonPropertyName("TaxNumber")] public string? Nif { get; set; }
    [JsonPropertyName("Type")] public string? Tipo { get; set; }
    [JsonPropertyName("CreditLimit")] public decimal? LimiteCredito { get; set; }
    [JsonPropertyName("IsActive")] public bool? Ativo { get; set; }
    [JsonPropertyName("Version")] public string? Versao { get; set; }

    /// <summary>O setter só é chamado quando a propriedade existe no JSON (mesmo com valor null).</summary>
    [JsonPropertyName("Email")]
    public string? Email
    {
        get => _email;
        set { _email = value; EmailInformado = true; }
    }

    [JsonIgnore] public bool EmailInformado { get; private set; }
}

/// <summary>Parâmetros de query do GET /api/partners.</summary>
public class ListarParceirosConsulta
{
    public string? Search { get; set; }
    public string? Type { get; set; }
    public bool IncludeInactive { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
