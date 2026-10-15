using Microsoft.EntityFrameworkCore;
using Parceiros.Dominio;

namespace Parceiros.Infra;

public class ContextoParceiros(DbContextOptions<ContextoParceiros> opcoes) : DbContext(opcoes)
{
    public const string IndiceNifUnico = "UX_Parceiros_Nif";

    public DbSet<Parceiro> Parceiros => Set<Parceiro>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configuracao)
    {
        // Datas guardadas em UTC e lidas de volta como UTC.
        configuracao.Properties<DateTime>().HaveConversion<DataUtcConversor>();
    }

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<Parceiro>(e =>
        {
            e.ToTable("Parceiros");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).ValueGeneratedNever();

            // Collation explícita: a pesquisa por nome não distingue maiúsculas de minúsculas
            // independentemente da collation por defeito da base de dados.
            e.Property(p => p.Nome).HasMaxLength(100).IsRequired().UseCollation("SQL_Latin1_General_CP1_CI_AS");

            e.Property(p => p.Nif).HasColumnType("char(9)").IsUnicode(false).IsRequired();
            e.Property(p => p.Tipo).HasConversion<string>(
                t => t.ParaApi(),
                s => s == "CUSTOMER" ? TipoParceiro.Cliente : s == "SUPPLIER" ? TipoParceiro.Fornecedor : TipoParceiro.Ambos)
                .HasMaxLength(10).IsUnicode(false).IsRequired();
            e.Property(p => p.Email).HasMaxLength(Regras.EmailTamanhoMaximo);
            e.Property(p => p.LimiteCredito).HasColumnType("decimal(18,2)");
            e.Property(p => p.Versao).IsRowVersion();
            e.Property(p => p.CriadoEm).HasColumnType("datetime2(7)");
            e.Property(p => p.AtualizadoEm).HasColumnType("datetime2(7)");

            // A unicidade do NIF é garantida pela base de dados (inclui inativos), não por lock em memória.
            e.HasIndex(p => p.Nif).IsUnique().HasDatabaseName(IndiceNifUnico);
            e.HasIndex(p => new { p.Nome, p.Id });
            e.ToTable(t => t.HasCheckConstraint("CK_Parceiros_Nif", "[Nif] NOT LIKE '%[^0-9]%' AND LEN([Nif]) = 9"));
            e.ToTable(t => t.HasCheckConstraint("CK_Parceiros_Limite", "[LimiteCredito] >= 0 AND [LimiteCredito] <= 1000000"));
        });
    }
}

internal class DataUtcConversor() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
