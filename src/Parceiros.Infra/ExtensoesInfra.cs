using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Parceiros.Infra;

public static class ExtensoesInfra
{
    public static IServiceCollection AdicionarInfra(this IServiceCollection servicos, string stringLigacao)
    {
        servicos.AddDbContext<ContextoParceiros>(o => o.UseSqlServer(stringLigacao));
        servicos.AddScoped<RepositorioParceiros>();
        return servicos;
    }
}
