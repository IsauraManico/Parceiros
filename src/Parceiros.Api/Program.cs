using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parceiros.Api.Erros;
using Parceiros.Api.Servicos;
using Parceiros.Infra;

var builder = WebApplication.CreateBuilder(args);

var stringLigacao = builder.Configuration.GetConnectionString("Parceiros")
    ?? throw new InvalidOperationException("Falta a connection string 'Parceiros'.");

builder.Services.AdicionarInfra(stringLigacao);
builder.Services.AddScoped<ServicoParceiros>();

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null) // nomes exatos do enunciado
    .ConfigureApiBehaviorOptions(o =>
    {
        // Erros de binding (JSON malformado, tipos errados, Page=abc, Id que não é GUID) → 400 no mesmo formato.
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var erros = ctx.ModelState
                .Where(e => e.Value is { Errors.Count: > 0 })
                .ToDictionary(
                    e => string.IsNullOrEmpty(e.Key) ? "Body" : e.Key.TrimStart('$', '.'),
                    e => e.Value!.Errors.Select(_ => "Valor inválido ou malformado.").ToArray());
            var problema = TratadorExcecoes.Criar(400, "Pedido inválido", "Um ou mais campos são inválidos.", erros);
            return new ObjectResult(problema) { StatusCode = 400, ContentTypes = { "application/problem+json" } };
        };
    });

builder.Services.AddExceptionHandler<TratadorExcecoes>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // Em desenvolvimento aplica as migrações e insere dados de exemplo ao arrancar.
    using var escopo = app.Services.CreateScope();
    var contexto = escopo.ServiceProvider.GetRequiredService<ContextoParceiros>();
    await contexto.Database.MigrateAsync();
    await DadosExemplo.InserirAsync(contexto);
}

app.MapControllers();

app.Run();

public partial class Program;
