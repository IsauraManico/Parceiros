using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Parceiros.Infra;

namespace Parceiros.Testes;

/// <summary>
/// Sobe a API em memória contra um SQL Server real, numa base de dados própria (criada pelas migrações
/// e apagada no fim). Cada pedido HTTP usa o seu próprio DbContext/ligação, por isso os testes de
/// concorrência exercitam a base de dados e não um lock em memória.
/// </summary>
public class FabricaApi : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string ConexaoBase = Environment.GetEnvironmentVariable("PARCEIROS_TESTES_CONEXAO")
        ?? "Server=localhost,1433;User Id=sa;Password=Parceiros#Dev2026;TrustServerCertificate=True;Encrypt=False";

    private static int _contadorNif = Random.Shared.Next(100_000_000, 500_000_000);

    private readonly string _nomeBd = $"ParceirosTestes_{Guid.NewGuid():N}";
    private string StringLigacao => new SqlConnectionStringBuilder(ConexaoBase) { InitialCatalog = _nomeBd }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); // sem migração automática nem dados de exemplo
        builder.UseSetting("ConnectionStrings:Parceiros", StringLigacao);
    }

    public async Task InitializeAsync()
    {
        using var escopo = Services.CreateScope();
        await escopo.ServiceProvider.GetRequiredService<ContextoParceiros>().Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        using (var escopo = Services.CreateScope())
            await escopo.ServiceProvider.GetRequiredService<ContextoParceiros>().Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }

    /// <summary>NIF único (9 dígitos) para o teste não colidir com outros.</summary>
    public static string NovoNif() => Interlocked.Increment(ref _contadorNif).ToString("D9");

    public static string NovoNome(string prefixo = "Teste") => $"{prefixo} {Guid.NewGuid():N}";

    public Task<HttpResponseMessage> Criar(object corpo) => CreateClient().PostAsJsonAsync("/api/partners", corpo);

    public Task<HttpResponseMessage> CriarJson(Dictionary<string, object?> corpo) => Criar(corpo);

    public async Task<JsonElement> CriarOk(string? nome = null, string? nif = null, string tipo = "CUSTOMER",
        string? email = null, decimal limite = 100m)
    {
        var r = await Criar(new { Name = nome ?? NovoNome(), TaxNumber = nif ?? NovoNif(), Type = tipo, Email = email, CreditLimit = limite });
        Assert.Equal(System.Net.HttpStatusCode.Created, r.StatusCode);
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    public Task<HttpResponseMessage> Atualizar(JsonElement atual, Action<Dictionary<string, object?>>? alterar = null)
    {
        var corpo = new Dictionary<string, object?>
        {
            ["Name"] = atual.GetProperty("Name").GetString(),
            ["TaxNumber"] = atual.GetProperty("TaxNumber").GetString(),
            ["Type"] = atual.GetProperty("Type").GetString(),
            ["Email"] = atual.GetProperty("Email").GetString(),
            ["CreditLimit"] = atual.GetProperty("CreditLimit").GetDecimal(),
            ["IsActive"] = atual.GetProperty("IsActive").GetBoolean(),
            ["Version"] = atual.GetProperty("Version").GetString()
        };
        alterar?.Invoke(corpo);
        return CreateClient().PutAsJsonAsync($"/api/partners/{atual.GetProperty("Id").GetGuid()}", corpo);
    }

    public async Task<JsonElement> Obter(JsonElement entidade)
    {
        var r = await CreateClient().GetAsync($"/api/partners/{entidade.GetProperty("Id").GetGuid()}");
        Assert.Equal(System.Net.HttpStatusCode.OK, r.StatusCode);
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async Task<JsonElement> Listar(string consulta)
    {
        var r = await CreateClient().GetAsync("/api/partners" + consulta);
        Assert.Equal(System.Net.HttpStatusCode.OK, r.StatusCode);
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static List<Guid> Ids(JsonElement pagina) =>
        pagina.GetProperty("Items").EnumerateArray().Select(i => i.GetProperty("Id").GetGuid()).ToList();
}
