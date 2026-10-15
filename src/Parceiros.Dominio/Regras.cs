using System.Net.Mail;
using System.Text;

namespace Parceiros.Dominio;

/// <summary>Normalização e validação dos campos de um parceiro (regras simplificadas do exercício).</summary>
public static class Regras
{
    public const decimal LimiteCreditoMaximo = 1_000_000m;
    public const int EmailTamanhoMaximo = 254;

    /// <summary>Remove espaços, pontos e hífenes do NIF. Não valida o resultado.</summary>
    public static string RemoverSeparadoresNif(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto)
            if (c != ' ' && c != '.' && c != '-') sb.Append(c);
        return sb.ToString();
    }

    public static bool ValidarNif(string? bruto, out string nifNormalizado)
    {
        nifNormalizado = string.Empty;
        if (bruto is null) return false;
        var texto = RemoverSeparadoresNif(bruto);
        // Só dígitos ASCII (char.IsDigit aceitaria dígitos de outros alfabetos).
        if (texto.Length != 9 || !texto.All(c => c is >= '0' and <= '9')) return false;
        nifNormalizado = texto;
        return true;
    }

    /// <summary>Valida e normaliza todos os campos. Acumula todos os erros encontrados.</summary>
    public static DadosValidados Validar(
        string? nome, string? nif, string? tipo, string? email, decimal? limiteCredito)
    {
        var erros = new Dictionary<string, List<string>>();
        void Erro(string campo, string msg)
        {
            if (!erros.TryGetValue(campo, out var lista)) erros[campo] = lista = [];
            lista.Add(msg);
        }

        var nomeLimpo = nome?.Trim();
        if (string.IsNullOrEmpty(nomeLimpo))
            Erro("Name", "O nome é obrigatório.");
        else if (nomeLimpo.Length is < 2 or > 100)
            Erro("Name", "O nome deve ter entre 2 e 100 caracteres.");

        string nifLimpo = string.Empty;
        if (string.IsNullOrWhiteSpace(nif))
            Erro("TaxNumber", "O NIF é obrigatório.");
        else if (!ValidarNif(nif, out nifLimpo))
            Erro("TaxNumber", "O NIF deve ter exatamente 9 dígitos (espaços, pontos e hífenes são ignorados).");

        if (!TipoParceiroExtensoes.TentarLerDaApi(tipo, out var tipoLido))
            Erro("Type", "O tipo deve ser CUSTOMER, SUPPLIER ou BOTH.");

        string? emailLimpo = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            emailLimpo = email.Trim();
            if (!EmailValido(emailLimpo))
                Erro("Email", "O formato do email é inválido.");
        }

        if (limiteCredito is null)
            Erro("CreditLimit", "O limite de crédito é obrigatório.");
        else if (limiteCredito < 0 || limiteCredito > LimiteCreditoMaximo)
            Erro("CreditLimit", "O limite de crédito deve estar entre 0 e 1 000 000.");
        else if (decimal.Round(limiteCredito.Value, 2) != limiteCredito.Value)
            Erro("CreditLimit", "O limite de crédito admite no máximo duas casas decimais.");

        if (erros.Count > 0)
            throw new ExcecaoValidacao(erros.ToDictionary(e => e.Key, e => e.Value.ToArray()));

        return new DadosValidados(nomeLimpo!, nifLimpo, tipoLido, emailLimpo, limiteCredito!.Value);
    }

    public static bool EmailValido(string email)
    {
        if (email.Length > EmailTamanhoMaximo || email.Any(char.IsWhiteSpace)) return false;
        // MailAddress aceita "Nome <a@b.c>"; exigimos que o endereço seja exatamente o texto recebido.
        if (!MailAddress.TryCreate(email, out var endereco) || endereco.Address != email) return false;
        return endereco.Host.Contains('.');
    }
}

public record DadosValidados(string Nome, string Nif, TipoParceiro Tipo, string? Email, decimal LimiteCredito);
