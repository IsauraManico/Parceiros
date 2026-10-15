using Microsoft.AspNetCore.Mvc;
using Parceiros.Api.Modelos;
using Parceiros.Api.Servicos;

namespace Parceiros.Api.Controllers;

[ApiController]
[Route("api/partners")]
[Produces("application/json")]
public class ParceirosController(ServicoParceiros servico) : ControllerBase
{
    /// <summary>Cria uma entidade (cliente, fornecedor ou ambos).</summary>
    [HttpPost]
    [ProducesResponseType<ParceiroResposta>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Criar([FromBody] CriarParceiroPedido pedido, CancellationToken ct)
    {
        var parceiro = await servico.CriarAsync(pedido, ct);
        return CreatedAtAction(nameof(Obter), new { id = parceiro.Id }, ParceiroResposta.De(parceiro));
    }

    /// <summary>Obtém uma entidade pelo Id, mesmo que esteja inativa.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ParceiroResposta>> Obter(Guid id, CancellationToken ct) =>
        ParceiroResposta.De(await servico.ObterAsync(id, ct));

    /// <summary>Lista entidades com pesquisa, filtro por tipo e paginação.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginaResposta>> Listar([FromQuery] ListarParceirosConsulta consulta, CancellationToken ct) =>
        await servico.ListarAsync(consulta, ct);

    /// <summary>Atualiza uma entidade (inclui desativar/reativar). Exige a Version atual.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ParceiroResposta>> Atualizar(Guid id, [FromBody] AtualizarParceiroPedido pedido, CancellationToken ct) =>
        ParceiroResposta.De(await servico.AtualizarAsync(id, pedido, ct));
}
