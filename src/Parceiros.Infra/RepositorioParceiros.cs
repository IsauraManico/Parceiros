using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Parceiros.Dominio;

namespace Parceiros.Infra;

public record FiltroListagem(string? Pesquisa, TipoParceiro? Tipo, bool IncluirInativos, int Pagina, int TamanhoPagina);

public record ResultadoPaginado(IReadOnlyList<Parceiro> Itens, int Total);

public class RepositorioParceiros(ContextoParceiros contexto)
{
    public async Task AdicionarAsync(Parceiro parceiro, CancellationToken ct = default)
    {
        contexto.Parceiros.Add(parceiro);
        await GuardarAsync(ct);
    }

    public Task<Parceiro?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        contexto.Parceiros.FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <summary>
    /// Guarda alterações a um parceiro já carregado. O UPDATE leva "WHERE Versao = @versaoOriginal"
    /// (concorrência otimista na própria base de dados), por isso só uma de duas atualizações
    /// com a mesma versão é confirmada.
    /// </summary>
    public async Task AtualizarAsync(Parceiro parceiro, byte[] versaoRecebida, CancellationToken ct = default)
    {
        contexto.Entry(parceiro).Property(p => p.Versao).OriginalValue = versaoRecebida;
        await GuardarAsync(ct);
    }

    public async Task<ResultadoPaginado> ListarAsync(FiltroListagem filtro, CancellationToken ct = default)
    {
        IQueryable<Parceiro> consulta = contexto.Parceiros.AsNoTracking();

        if (!filtro.IncluirInativos)
            consulta = consulta.Where(p => p.Ativo);

        // CUSTOMER inclui CUSTOMER e BOTH; SUPPLIER inclui SUPPLIER e BOTH; BOTH inclui apenas BOTH.
        if (filtro.Tipo is { } tipo)
            consulta = tipo == TipoParceiro.Ambos
                ? consulta.Where(p => p.Tipo == TipoParceiro.Ambos)
                : consulta.Where(p => p.Tipo == tipo || p.Tipo == TipoParceiro.Ambos);

        var pesquisa = filtro.Pesquisa?.Trim();
        if (!string.IsNullOrEmpty(pesquisa))
        {
            // Para o ramo do NIF normalizamos os mesmos separadores; se sobrar vazio, o ramo é ignorado
            // (um Contains("") faria corresponder todos os NIF).
            var nifPesquisa = Regras.RemoverSeparadoresNif(pesquisa);
            consulta = nifPesquisa.Length == 0
                ? consulta.Where(p => p.Nome.Contains(pesquisa))
                : consulta.Where(p => p.Nome.Contains(pesquisa) || p.Nif.Contains(nifPesquisa));
        }

        var total = await consulta.CountAsync(ct);
        var itens = await consulta
            .OrderBy(p => p.Nome).ThenBy(p => p.Id)
            .Skip((filtro.Pagina - 1) * filtro.TamanhoPagina)
            .Take(filtro.TamanhoPagina)
            .ToListAsync(ct);

        return new ResultadoPaginado(itens, total);
    }

    private async Task GuardarAsync(CancellationToken ct)
    {
        try
        {
            await contexto.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            contexto.ChangeTracker.Clear();
            throw new ExcecaoConflito("A versão enviada está desatualizada: o registo foi alterado por outro pedido. Leia-o novamente e repita a atualização.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            contexto.ChangeTracker.Clear();
            throw new ExcecaoConflito("Já existe uma entidade registada com este NIF.");
        }
    }
}
