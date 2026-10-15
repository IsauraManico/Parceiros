using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Parceiros.Testes;

/// <summary>Cenários mínimos do enunciado (1 a 7) mais casos de erro, contra SQL Server real.</summary>
public class ApiTestes(FabricaApi api) : IClassFixture<FabricaApi>
{
    // ---------- Cenário 1 ----------
    [Fact]
    public async Task C1_criar_normaliza_dados_e_devolve_201_com_Location()
    {
        var r = await api.Criar(new { Name = "  Ana Silva  ", TaxNumber = "001.234.567", Type = "CUSTOMER", Email = "  ana@exemplo.pt ", CreditLimit = 10.5m });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var corpo = await r.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Ana Silva", corpo.GetProperty("Name").GetString());
        Assert.Equal("001234567", corpo.GetProperty("TaxNumber").GetString());
        Assert.Equal("ana@exemplo.pt", corpo.GetProperty("Email").GetString());
        Assert.True(corpo.GetProperty("IsActive").GetBoolean());
        Assert.False(string.IsNullOrEmpty(corpo.GetProperty("Version").GetString()));
        Assert.Equal(DateTimeKind.Utc, corpo.GetProperty("CreatedAt").GetDateTime().Kind);
        Assert.EndsWith($"/api/partners/{corpo.GetProperty("Id").GetGuid()}", r.Headers.Location!.ToString());
    }

    [Fact]
    public async Task C1_NIF_duplicado_noutra_formatacao_devolve_409_mesmo_apos_desativar()
    {
        var nif = FabricaApi.NovoNif();
        var criada = await api.CriarOk(nif: nif);
        var formatado = $"{nif[..3]} {nif[3..6]} {nif[6..]}";

        var duplicado = await api.Criar(new { Name = "Outra Pessoa", TaxNumber = formatado, Type = "SUPPLIER", CreditLimit = 1 });
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);

        var desativar = await api.Atualizar(criada, c => c["IsActive"] = false);
        Assert.Equal(HttpStatusCode.OK, desativar.StatusCode);

        var depois = await api.Criar(new { Name = "Outra Pessoa", TaxNumber = formatado, Type = "SUPPLIER", CreditLimit = 1 });
        Assert.Equal(HttpStatusCode.Conflict, depois.StatusCode); // o NIF continua reservado
    }

    [Fact]
    public async Task C1_email_so_com_espacos_e_guardado_como_null()
    {
        var c = await api.CriarOk(email: "   ");
        Assert.Equal(JsonValueKind.Null, (await api.Obter(c)).GetProperty("Email").ValueKind);
    }

    // ---------- Cenário 2 ----------
    [Fact]
    public async Task C2_filtros_de_tipo()
    {
        var marca = Guid.NewGuid().ToString("N")[..8];
        var cliente = await api.CriarOk($"Cli {marca}", tipo: "CUSTOMER");
        var fornecedor = await api.CriarOk($"For {marca}", tipo: "SUPPLIER");
        var ambos = await api.CriarOk($"Amb {marca}", tipo: "BOTH");
        Guid Id(JsonElement e) => e.GetProperty("Id").GetGuid();

        var pc = FabricaApi.Ids(await api.Listar($"?Search={marca}&Type=CUSTOMER"));
        var ps = FabricaApi.Ids(await api.Listar($"?Search={marca}&Type=SUPPLIER"));
        var pb = FabricaApi.Ids(await api.Listar($"?Search={marca}&Type=BOTH"));

        Assert.Equivalent(new[] { Id(cliente), Id(ambos) }, pc);
        Assert.Equivalent(new[] { Id(fornecedor), Id(ambos) }, ps);
        Assert.Equal(new[] { Id(ambos) }, pb);
        Assert.DoesNotContain(Id(cliente), ps); // um CUSTOMER não aparece no filtro SUPPLIER
    }

    // ---------- Cenário 3 ----------
    [Fact]
    public async Task C3_desativar_e_reativar()
    {
        var marca = Guid.NewGuid().ToString("N")[..8];
        var c = await api.CriarOk($"Ativ {marca}");
        var id = c.GetProperty("Id").GetGuid();

        var inativa = await (await api.Atualizar(c, x => x["IsActive"] = false)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(inativa.GetProperty("IsActive").GetBoolean());
        Assert.DoesNotContain(id, FabricaApi.Ids(await api.Listar($"?Search={marca}")));
        Assert.Contains(id, FabricaApi.Ids(await api.Listar($"?Search={marca}&IncludeInactive=true")));
        Assert.False((await api.Obter(c)).GetProperty("IsActive").GetBoolean()); // continua acessível por Id

        var reativada = await (await api.Atualizar(inativa, x => x["IsActive"] = true)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(reativada.GetProperty("IsActive").GetBoolean());
        Assert.Contains(id, FabricaApi.Ids(await api.Listar($"?Search={marca}")));
    }

    // ---------- Cenário 4 ----------
    [Fact]
    public async Task C4_atualizar_mantendo_o_proprio_NIF_e_permitido_e_nova_versao_e_devolvida()
    {
        var c = await api.CriarOk(email: "a@b.pt");
        var r = await api.Atualizar(c, x => { x["Name"] = "  Novo Nome  "; x["CreditLimit"] = 999.99m; });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var novo = await r.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Novo Nome", novo.GetProperty("Name").GetString());
        Assert.NotEqual(c.GetProperty("Version").GetString(), novo.GetProperty("Version").GetString());
        Assert.Equal(c.GetProperty("CreatedAt").GetDateTime(), novo.GetProperty("CreatedAt").GetDateTime()); // CreatedAt não muda
        Assert.True(novo.GetProperty("UpdatedAt").GetDateTime() >= c.GetProperty("UpdatedAt").GetDateTime());
        Assert.Equal(c.GetProperty("Id").GetGuid(), novo.GetProperty("Id").GetGuid());
    }

    [Fact]
    public async Task C4_mudar_para_NIF_de_outra_entidade_devolve_409_e_mantem_dados()
    {
        var a = await api.CriarOk();
        var b = await api.CriarOk();

        var r = await api.Atualizar(b, x => { x["TaxNumber"] = a.GetProperty("TaxNumber").GetString(); x["Name"] = "Nome Alterado"; });
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);

        var depois = await api.Obter(b);
        Assert.Equal(b.GetProperty("TaxNumber").GetString(), depois.GetProperty("TaxNumber").GetString());
        Assert.Equal(b.GetProperty("Name").GetString(), depois.GetProperty("Name").GetString());
        Assert.Equal(b.GetProperty("Version").GetString(), depois.GetProperty("Version").GetString());
    }

    // ---------- Cenário 5 ----------
    [Fact]
    public async Task C5_pedidos_simultaneos_com_o_mesmo_NIF_criam_um_unico_registo()
    {
        var nif = FabricaApi.NovoNif();
        var marca = Guid.NewGuid().ToString("N")[..8];
        var arranque = new TaskCompletionSource();

        var tarefas = Enumerable.Range(0, 10).Select(async i =>
        {
            await arranque.Task; // todos largam ao mesmo tempo, cada um com a sua ligação
            return (await api.Criar(new { Name = $"Conc {marca} {i}", TaxNumber = nif, Type = "BOTH", CreditLimit = 1 })).StatusCode;
        }).ToList();
        arranque.SetResult();
        var codigos = await Task.WhenAll(tarefas);

        Assert.Single(codigos, HttpStatusCode.Created);
        Assert.Equal(9, codigos.Count(c => c == HttpStatusCode.Conflict));
        Assert.Equal(1, (await api.Listar($"?Search={nif}")).GetProperty("TotalCount").GetInt32());
    }

    [Fact]
    public async Task C5_duas_atualizacoes_com_a_mesma_versao_uma_200_e_outra_409()
    {
        var c = await api.CriarOk();
        var arranque = new TaskCompletionSource();

        var tarefas = new[] { "Alteracao A", "Alteracao B" }.Select(async nome =>
        {
            await arranque.Task;
            var r = await api.Atualizar(c, x => x["Name"] = nome);
            return (r.StatusCode, nome);
        }).ToList();
        arranque.SetResult();
        var resultados = await Task.WhenAll(tarefas);

        Assert.Single(resultados, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(resultados, r => r.StatusCode == HttpStatusCode.Conflict);
        var vencedor = resultados.Single(r => r.StatusCode == HttpStatusCode.OK).nome;
        Assert.Equal(vencedor, (await api.Obter(c)).GetProperty("Name").GetString()); // o perdedor não sobrescreveu
    }

    [Fact]
    public async Task C5_versao_antiga_depois_de_outra_atualizacao_devolve_409()
    {
        var c = await api.CriarOk();
        Assert.Equal(HttpStatusCode.OK, (await api.Atualizar(c, x => x["Name"] = "Primeira")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await api.Atualizar(c, x => x["Name"] = "Segunda")).StatusCode);
    }

    // ---------- Cenário 6 ----------
    [Theory]
    [InlineData("CreditLimit", 10.999)]
    [InlineData("CreditLimit", -1)]
    [InlineData("CreditLimit", 1000000.01)]
    [InlineData("TaxNumber", "12345678A")]
    [InlineData("TaxNumber", "12345678")]
    [InlineData("Type", "OUTRO")]
    [InlineData("Name", "A")]
    [InlineData("Email", "nao-e-email")]
    public async Task C6_criacao_com_valores_invalidos_devolve_400(string campo, object valor)
    {
        var corpo = new Dictionary<string, object?> { ["Name"] = "Valido", ["TaxNumber"] = FabricaApi.NovoNif(), ["Type"] = "BOTH", ["CreditLimit"] = 1 };
        corpo[campo] = valor;
        var r = await api.CriarJson(corpo);
        await AssertErro(r, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task C6_atualizacao_invalida_nao_altera_o_registo()
    {
        var c = await api.CriarOk();
        var r = await api.Atualizar(c, x => { x["Name"] = "Nome Novo Valido"; x["CreditLimit"] = 10.999m; });
        await AssertErro(r, HttpStatusCode.BadRequest);

        var depois = await api.Obter(c);
        Assert.Equal(c.GetProperty("Name").GetString(), depois.GetProperty("Name").GetString());
        Assert.Equal(c.GetProperty("Version").GetString(), depois.GetProperty("Version").GetString());
    }

    [Theory]
    [InlineData("?Page=0")]
    [InlineData("?Page=-1")]
    [InlineData("?Page=abc")]
    [InlineData("?PageSize=0")]
    [InlineData("?PageSize=101")]
    [InlineData("?Type=INVALIDO")]
    [InlineData("?IncludeInactive=talvez")]
    public async Task C6_filtros_invalidos_devolvem_400(string consulta) =>
        await AssertErro(await api.CreateClient().GetAsync("/api/partners" + consulta), HttpStatusCode.BadRequest);

    [Theory]
    [InlineData("nao-e-base64!!")]
    [InlineData("AAAA")]          // Base64 válido, mas não tem 8 bytes
    [InlineData("")]
    [InlineData(null)]
    public async Task C6_versao_malformada_devolve_400_e_nao_altera(string? versao)
    {
        var c = await api.CriarOk();
        var r = await api.Atualizar(c, x => x["Version"] = versao);
        await AssertErro(r, HttpStatusCode.BadRequest);
        Assert.Equal(c.GetProperty("Version").GetString(), (await api.Obter(c)).GetProperty("Version").GetString());
    }

    [Fact]
    public async Task C6_PUT_exige_todos_os_campos_mas_Email_pode_ser_null()
    {
        var c = await api.CriarOk(email: "x@y.pt");

        // Email null explícito: permitido (limpa o email).
        var ok = await api.Atualizar(c, x => x["Email"] = null);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("Email").ValueKind);

        // Campos em falta: 400.
        var id = c.GetProperty("Id").GetGuid();
        foreach (var corpo in new[] {
            """{"Name":"Xpto","TaxNumber":"123456789","Type":"BOTH","CreditLimit":1,"Version":"AAAAAAAAB9E="}""",              // sem IsActive nem Email
            """{"TaxNumber":"123456789","Type":"BOTH","Email":null,"CreditLimit":1,"IsActive":true,"Version":"AAAAAAAAB9E="}""", // sem Name
            """{"Name":"Xpto","TaxNumber":"123456789","Type":"BOTH","Email":null,"CreditLimit":1,"IsActive":true}""" })     // sem Version
        {
            var r = await api.CreateClient().PutAsync($"/api/partners/{id}", new StringContent(corpo, Encoding.UTF8, "application/json"));
            await AssertErro(r, HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task C6_JSON_malformado_devolve_400_sem_stack_trace()
    {
        var r = await api.CreateClient().PostAsync("/api/partners", new StringContent("{ isto nao e json", Encoding.UTF8, "application/json"));
        await AssertErro(r, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GET_id_inexistente_devolve_404_e_id_invalido_devolve_400_ou_404()
    {
        var r = await api.CreateClient().GetAsync($"/api/partners/{Guid.NewGuid()}");
        await AssertErro(r, HttpStatusCode.NotFound);

        var inv = await api.CreateClient().GetAsync("/api/partners/nao-e-guid");
        Assert.True(inv.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PUT_id_inexistente_devolve_404()
    {
        var c = await api.CriarOk();
        var corpo = new { Name = "Xpto", TaxNumber = FabricaApi.NovoNif(), Type = "BOTH", Email = (string?)null, CreditLimit = 1, IsActive = true, Version = c.GetProperty("Version").GetString() };
        var r = await api.CreateClient().PutAsJsonAsync($"/api/partners/{Guid.NewGuid()}", corpo);
        await AssertErro(r, HttpStatusCode.NotFound);
    }

    // ---------- Cenário 7 ----------
    [Fact]
    public async Task C7_filtros_combinados_total_antes_da_paginacao_e_desempate_por_Id()
    {
        var marca = Guid.NewGuid().ToString("N")[..8];
        // Três com o mesmo nome (desempate por Id), um BOTH, um SUPPLIER, um inativo.
        var iguais = new List<JsonElement>();
        for (var i = 0; i < 3; i++) iguais.Add(await api.CriarOk($"Zeta {marca}", tipo: "CUSTOMER"));
        await api.CriarOk($"Alfa {marca}", tipo: "BOTH");
        await api.CriarOk($"Beta {marca}", tipo: "SUPPLIER");
        var inativo = await api.CriarOk($"Gama {marca}", tipo: "CUSTOMER");
        await api.Atualizar(inativo, x => x["IsActive"] = false);

        // CUSTOMER (+BOTH), ativos: Alfa(BOTH), Zeta x3 = 4 itens; o fornecedor e o inativo ficam de fora.
        var p1 = await api.Listar($"?Search={marca}&Type=CUSTOMER&Page=1&PageSize=2");
        var p2 = await api.Listar($"?Search={marca}&Type=CUSTOMER&Page=2&PageSize=2");
        Assert.Equal(4, p1.GetProperty("TotalCount").GetInt32());
        Assert.Equal(4, p2.GetProperty("TotalCount").GetInt32());
        Assert.Equal(1, p1.GetProperty("Page").GetInt32());
        Assert.Equal(2, p1.GetProperty("PageSize").GetInt32());

        var todos = FabricaApi.Ids(p1).Concat(FabricaApi.Ids(p2)).ToList();
        Assert.Equal(4, todos.Distinct().Count()); // sem repetições entre páginas
        var nomes = p1.GetProperty("Items").EnumerateArray().Concat(p2.GetProperty("Items").EnumerateArray())
            .Select(i => i.GetProperty("Name").GetString()).ToList();
        Assert.Equal($"Alfa {marca}", nomes[0]);

        // Os três "Zeta" aparecem ordenados por Id (compara na ordem do SQL Server para uniqueidentifier).
        var idsZeta = iguais.Select(e => e.GetProperty("Id").GetGuid()).ToList();
        var zetaDevolvidos = todos.Where(idsZeta.Contains).ToList();
        Assert.Equal(idsZeta.OrderBy(g => g, new OrdemGuidSqlServer()).ToList(), zetaDevolvidos);
    }

    [Fact]
    public async Task C7_pagina_alem_dos_resultados_devolve_Items_vazio_e_TotalCount_correto()
    {
        var marca = Guid.NewGuid().ToString("N")[..8];
        await api.CriarOk($"Unico {marca}");
        var p = await api.Listar($"?Search={marca}&Page=5&PageSize=10");
        Assert.Empty(p.GetProperty("Items").EnumerateArray());
        Assert.Equal(1, p.GetProperty("TotalCount").GetInt32());
    }

    [Fact]
    public async Task C7_Search_por_nome_nao_corresponde_a_todos_pelo_ramo_do_NIF()
    {
        var marca = Guid.NewGuid().ToString("N")[..8];
        var ana = await api.CriarOk($"Ana {marca}");
        await api.CriarOk($"Bruno {marca}");

        // "Ana" não é NIF: só corresponde por nome (sem distinguir maiúsculas).
        var porNome = FabricaApi.Ids(await api.Listar($"?Search=ANA {marca}"));
        Assert.Equal(new[] { ana.GetProperty("Id").GetGuid() }, porNome);

        // Pesquisa só com separadores normaliza para vazio: não pode devolver "todos os NIF".
        var soSeparadores = await api.Listar("?Search=...---&PageSize=100");
        Assert.DoesNotContain(ana.GetProperty("Id").GetGuid(), FabricaApi.Ids(soSeparadores));
    }

    [Fact]
    public async Task C7_Search_por_NIF_aceita_separadores_e_ocorrencia_parcial()
    {
        var nif = FabricaApi.NovoNif();
        var c = await api.CriarOk(nif: nif);
        var formatado = $"{nif[..3]}.{nif[3..6]}-{nif[6..]}";

        Assert.Contains(c.GetProperty("Id").GetGuid(), FabricaApi.Ids(await api.Listar($"?Search={formatado}")));
        Assert.Contains(c.GetProperty("Id").GetGuid(), FabricaApi.Ids(await api.Listar($"?Search={nif[2..7]}")));
    }

    // ---------- Auxiliares ----------
    private static async Task AssertErro(HttpResponseMessage r, HttpStatusCode esperado)
    {
        Assert.Equal(esperado, r.StatusCode);
        var texto = await r.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(texto).RootElement;
        Assert.Equal((int)esperado, json.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("detail").GetString()));
        Assert.DoesNotContain("   at ", texto);        // sem stack trace
        Assert.DoesNotContain("Exception", texto);
    }

    /// <summary>Ordem de comparação de uniqueidentifier no SQL Server (últimos bytes primeiro).</summary>
    private class OrdemGuidSqlServer : IComparer<Guid>
    {
        private static readonly int[] Posicoes = [10, 11, 12, 13, 14, 15, 8, 9, 6, 7, 4, 5, 0, 1, 2, 3];
        public int Compare(Guid x, Guid y)
        {
            var a = x.ToByteArray(); var b = y.ToByteArray();
            foreach (var p in Posicoes)
            {
                var c = a[p].CompareTo(b[p]);
                if (c != 0) return c;
            }
            return 0;
        }
    }
}
