using System.Text.Json.Serialization;
using Parceiros.Dominio;

namespace Parceiros.Api.Modelos;

public class ParceiroResposta
{
    [JsonPropertyName("Id")] public Guid Id { get; init; }
    [JsonPropertyName("Name")] public string Nome { get; init; } = "";
    [JsonPropertyName("TaxNumber")] public string Nif { get; init; } = "";
    [JsonPropertyName("Type")] public string Tipo { get; init; } = "";
    [JsonPropertyName("Email")] public string? Email { get; init; }
    [JsonPropertyName("CreditLimit")] public decimal LimiteCredito { get; init; }
    [JsonPropertyName("IsActive")] public bool Ativo { get; init; }
    [JsonPropertyName("Version")] public string Versao { get; init; } = "";
    [JsonPropertyName("CreatedAt")] public DateTime CriadoEm { get; init; }
    [JsonPropertyName("UpdatedAt")] public DateTime AtualizadoEm { get; init; }

    public static ParceiroResposta De(Parceiro p) => new()
    {
        Id = p.Id, Nome = p.Nome, Nif = p.Nif, Tipo = p.Tipo.ParaApi(), Email = p.Email,
        LimiteCredito = p.LimiteCredito, Ativo = p.Ativo, Versao = Convert.ToBase64String(p.Versao),
        CriadoEm = p.CriadoEm, AtualizadoEm = p.AtualizadoEm
    };
}

public class PaginaResposta
{
    [JsonPropertyName("Items")] public IReadOnlyList<ParceiroResposta> Itens { get; init; } = [];
    [JsonPropertyName("TotalCount")] public int Total { get; init; }
    [JsonPropertyName("Page")] public int Pagina { get; init; }
    [JsonPropertyName("PageSize")] public int TamanhoPagina { get; init; }
}
