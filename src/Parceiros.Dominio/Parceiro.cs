namespace Parceiros.Dominio;

/// <summary>Cliente, fornecedor ou ambos. Existe um único registo por NIF.</summary>
public class Parceiro
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>NIF normalizado: exatamente 9 dígitos, guardado como texto (preserva zeros à esquerda).</summary>
    public string Nif { get; set; } = string.Empty;

    public TipoParceiro Tipo { get; set; }
    public string? Email { get; set; }
    public decimal LimiteCredito { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>rowversion do SQL Server, usado como token de concorrência otimista.</summary>
    public byte[] Versao { get; set; } = [];

    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
}
