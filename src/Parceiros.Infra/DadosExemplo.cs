using Microsoft.EntityFrameworkCore;
using Parceiros.Dominio;

namespace Parceiros.Infra;

/// <summary>Dados de exemplo para testes manuais. Só insere se a tabela estiver vazia.</summary>
public static class DadosExemplo
{
    public static async Task InserirAsync(ContextoParceiros contexto)
    {
        if (await contexto.Parceiros.AnyAsync()) return;

        var agora = DateTime.UtcNow;
        Parceiro Novo(string nome, string nif, TipoParceiro tipo, string? email, decimal limite, bool ativo = true) => new()
        {
            Id = Guid.NewGuid(), Nome = nome, Nif = nif, Tipo = tipo, Email = email,
            LimiteCredito = limite, Ativo = ativo, CriadoEm = agora, AtualizadoEm = agora
        };

        contexto.Parceiros.AddRange(
            Novo("Ana Silva", "001234567", TipoParceiro.Cliente, "ana.silva@exemplo.pt", 5000m),
            Novo("Anabela Costa", "123456789", TipoParceiro.Ambos, "anabela@exemplo.pt", 12000.50m),
            Novo("Bruno Fernandes", "234567891", TipoParceiro.Fornecedor, null, 0m),
            Novo("Metalúrgica do Norte, Lda", "345678912", TipoParceiro.Fornecedor, "geral@metalnorte.pt", 250000m),
            Novo("Carlos Mendes", "456789123", TipoParceiro.Cliente, "carlos@exemplo.pt", 750m),
            Novo("Ana Silva", "567891234", TipoParceiro.Cliente, null, 100m),
            Novo("Distribuidora Atlântico", "678912345", TipoParceiro.Ambos, "contacto@atlantico.pt", 1000000m),
            Novo("Empresa Inativa, SA", "789123456", TipoParceiro.Cliente, null, 0m, ativo: false));
        await contexto.SaveChangesAsync();
    }
}
