using Parceiros.Api.Modelos;
using Parceiros.Dominio;
using Parceiros.Infra;

namespace Parceiros.Api.Servicos;

public class ServicoParceiros(RepositorioParceiros repositorio)
{
    public const int TamanhoPaginaPadrao = 20;
    public const int TamanhoPaginaMaximo = 100;

    public async Task<Parceiro> CriarAsync(CriarParceiroPedido pedido, CancellationToken ct)
    {
        var dados = Regras.Validar(pedido.Nome, pedido.Nif, pedido.Tipo, pedido.Email, pedido.LimiteCredito);

        var agora = DateTime.UtcNow;
        var parceiro = new Parceiro
        {
            Id = Guid.NewGuid(),
            Nome = dados.Nome, Nif = dados.Nif, Tipo = dados.Tipo,
            Email = dados.Email, LimiteCredito = dados.LimiteCredito,
            Ativo = true, CriadoEm = agora, AtualizadoEm = agora
        };
        await repositorio.AdicionarAsync(parceiro, ct);
        return parceiro;
    }

    public async Task<Parceiro> ObterAsync(Guid id, CancellationToken ct) =>
        await repositorio.ObterPorIdAsync(id, ct) ?? throw NaoEncontrado(id);

    public async Task<Parceiro> AtualizarAsync(Guid id, AtualizarParceiroPedido pedido, CancellationToken ct)
    {
        // 1) Validar tudo antes de tocar no registo: uma atualização inválida não altera nada.
        var erros = new Dictionary<string, string[]>();
        if (!pedido.EmailInformado) erros["Email"] = ["O campo Email é obrigatório no corpo (pode ser null)."];
        if (pedido.Ativo is null) erros["IsActive"] = ["O campo IsActive é obrigatório."];

        byte[]? versao = null;
        if (string.IsNullOrWhiteSpace(pedido.Versao))
            erros["Version"] = ["A versão é obrigatória."];
        else if (!TentarLerVersao(pedido.Versao, out versao))
            erros["Version"] = ["A versão é inválida (token Base64 devolvido pela API)."];

        DadosValidados? dados = null;
        try
        {
            dados = Regras.Validar(pedido.Nome, pedido.Nif, pedido.Tipo, pedido.Email, pedido.LimiteCredito);
        }
        catch (ExcecaoValidacao ex)
        {
            foreach (var (campo, msgs) in ex.Erros)
                erros[campo] = erros.TryGetValue(campo, out var antigas) ? [.. antigas, .. msgs] : msgs;
        }
        if (erros.Count > 0) throw new ExcecaoValidacao(erros);

        // 2) Carregar e aplicar. O UPDATE só é confirmado se a versão na BD ainda for a recebida.
        var parceiro = await repositorio.ObterPorIdAsync(id, ct) ?? throw NaoEncontrado(id);
        parceiro.Nome = dados!.Nome;
        parceiro.Nif = dados.Nif;
        parceiro.Tipo = dados.Tipo;
        parceiro.Email = dados.Email;
        parceiro.LimiteCredito = dados.LimiteCredito;
        parceiro.Ativo = pedido.Ativo!.Value;
        parceiro.AtualizadoEm = DateTime.UtcNow;

        await repositorio.AtualizarAsync(parceiro, versao!, ct);
        return parceiro;
    }

    public async Task<PaginaResposta> ListarAsync(ListarParceirosConsulta consulta, CancellationToken ct)
    {
        var erros = new Dictionary<string, string[]>();

        TipoParceiro? tipo = null;
        if (!string.IsNullOrWhiteSpace(consulta.Type))
        {
            if (TipoParceiroExtensoes.TentarLerDaApi(consulta.Type, out var t)) tipo = t;
            else erros["Type"] = ["O tipo deve ser CUSTOMER, SUPPLIER ou BOTH."];
        }

        var pagina = consulta.Page ?? 1;
        if (pagina < 1) erros["Page"] = ["A página deve ser maior ou igual a 1."];

        var tamanho = consulta.PageSize ?? TamanhoPaginaPadrao;
        if (tamanho is < 1 or > TamanhoPaginaMaximo)
            erros["PageSize"] = [$"O tamanho da página deve estar entre 1 e {TamanhoPaginaMaximo}."];

        if (erros.Count > 0) throw new ExcecaoValidacao(erros);

        var resultado = await repositorio.ListarAsync(
            new FiltroListagem(consulta.Search, tipo, consulta.IncludeInactive, pagina, tamanho), ct);

        return new PaginaResposta
        {
            Itens = resultado.Itens.Select(ParceiroResposta.De).ToList(),
            Total = resultado.Total, Pagina = pagina, TamanhoPagina = tamanho
        };
    }

    private static bool TentarLerVersao(string texto, out byte[]? versao)
    {
        versao = null;
        var buffer = new byte[16];
        if (!Convert.TryFromBase64String(texto.Trim(), buffer, out var escritos) || escritos != 8) return false;
        versao = buffer[..8];
        return true;
    }

    private static ExcecaoNaoEncontrado NaoEncontrado(Guid id) =>
        new($"Não existe nenhuma entidade com o Id '{id}'.");
}
