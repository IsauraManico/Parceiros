using Parceiros.Dominio;

namespace Parceiros.Testes;

/// <summary>Testes unitários das regras de normalização/validação (sem base de dados).</summary>
public class RegrasTestes
{
    [Theory]
    [InlineData("001.234.567", "001234567")]
    [InlineData("001 234 567", "001234567")]
    [InlineData("001-234-567", "001234567")]
    [InlineData(" 123456789 ", "123456789")]
    public void Nif_normaliza_separadores_e_preserva_zeros(string bruto, string esperado)
    {
        Assert.True(Regras.ValidarNif(bruto, out var nif));
        Assert.Equal(esperado, nif);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("12345678A")]
    [InlineData("١٢٣٤٥٦٧٨٩")] // dígitos árabe-índicos: não são ASCII
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Nif_invalido(string? bruto) => Assert.False(Regras.ValidarNif(bruto, out _));

    [Fact]
    public void Validar_faz_trim_ao_nome_e_converte_email_vazio_em_null()
    {
        var d = Regras.Validar("  Ana Silva  ", "001.234.567", "customer", "   ", 10.99m);
        Assert.Equal("Ana Silva", d.Nome);
        Assert.Equal("001234567", d.Nif);
        Assert.Null(d.Email);
        Assert.Equal(TipoParceiro.Cliente, d.Tipo);
    }

    [Theory]
    [InlineData(10.999)]
    [InlineData(-0.01)]
    [InlineData(1000000.01)]
    public void Limite_de_credito_invalido_e_rejeitado(double valor) =>
        Assert.Throws<ExcecaoValidacao>(() => Regras.Validar("Ana", "123456789", "BOTH", null, (decimal)valor));

    [Theory]
    [InlineData(0)]
    [InlineData(1000000)]
    [InlineData(10.5)]
    public void Limite_de_credito_valido(double valor) =>
        Assert.Equal((decimal)valor, Regras.Validar("Ana", "123456789", "BOTH", null, (decimal)valor).LimiteCredito);

    [Theory]
    [InlineData("A")]
    [InlineData(" A ")]
    public void Nome_demasiado_curto_depois_do_trim(string nome) =>
        Assert.Throws<ExcecaoValidacao>(() => Regras.Validar(nome, "123456789", "BOTH", null, 1m));

    [Fact]
    public void Nome_com_mais_de_100_caracteres_e_rejeitado() =>
        Assert.Throws<ExcecaoValidacao>(() => Regras.Validar(new string('a', 101), "123456789", "BOTH", null, 1m));

    [Theory]
    [InlineData("a@b.pt", true)]
    [InlineData("Ana <a@b.pt>", false)]
    [InlineData("semarroba", false)]
    [InlineData("a@b", false)]
    [InlineData("a b@c.pt", false)]
    public void Email(string email, bool valido) => Assert.Equal(valido, Regras.EmailValido(email));

    [Theory]
    [InlineData("1")]
    [InlineData("OUTRO")]
    [InlineData("")]
    public void Tipo_invalido_e_rejeitado(string tipo) =>
        Assert.Throws<ExcecaoValidacao>(() => Regras.Validar("Ana", "123456789", tipo, null, 1m));
}
