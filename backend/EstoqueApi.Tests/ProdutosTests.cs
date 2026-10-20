using System.Net;
using System.Net.Http.Json;
using System.Text;
using EstoqueApi.Models;

namespace EstoqueApi.Tests;

public class ProdutosTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly ApiFactory _f;
    private HttpClient _c = null!;
    public ProdutosTests(ApiFactory f) => _f = f;

    public async Task InitializeAsync() => _c = await _f.ClienteLogado(ApiFactory.Admin);
    public Task DisposeAsync() => Task.CompletedTask;

    private static object Valido(string nome = "Produto Teste") =>
        new { nome, categoria = "Testes", preco = 10.5m, quantidade = 5, estoqueMinimo = 2 };

    private async Task<Produto> Criar(string nome = "Produto Teste")
    {
        var r = await _c.PostAsJsonAsync("/api/produtos", Valido(nome));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<Produto>())!;
    }

    // ---------- CRUD ----------

    [Fact]
    public async Task Listar_retorna_dados_iniciais()
    {
        var lista = await _c.GetFromJsonAsync<List<Produto>>("/api/produtos");
        Assert.NotNull(lista);
        Assert.True(lista.Count >= 4);
    }

    [Fact]
    public async Task Criar_obter_atualizar_excluir()
    {
        var p = await Criar("Ciclo Completo");
        Assert.True(p.Id > 0);

        var obtido = await _c.GetFromJsonAsync<Produto>($"/api/produtos/{p.Id}");
        Assert.Equal("Ciclo Completo", obtido!.Nome);

        var put = await _c.PutAsJsonAsync($"/api/produtos/{p.Id}", Valido("Ciclo Editado"));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("Ciclo Editado", (await put.Content.ReadFromJsonAsync<Produto>())!.Nome);

        Assert.Equal(HttpStatusCode.NoContent, (await _c.DeleteAsync($"/api/produtos/{p.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _c.GetAsync($"/api/produtos/{p.Id}")).StatusCode);
    }

    [Fact]
    public async Task Busca_filtra_por_nome()
    {
        await Criar("Item Unico XYZ");
        var lista = await _c.GetFromJsonAsync<List<Produto>>("/api/produtos?busca=unico xyz");
        Assert.Single(lista!);
    }

    [Fact]
    public async Task Categoria_vazia_vira_Geral_e_nome_e_aparado()
    {
        var r = await _c.PostAsJsonAsync("/api/produtos", new { nome = "  Espacos  ", categoria = " ", preco = 1, quantidade = 1, estoqueMinimo = 0 });
        var p = await r.Content.ReadFromJsonAsync<Produto>();
        Assert.Equal("Espacos", p!.Nome);
        Assert.Equal("Geral", p.Categoria);
    }

    // ---------- Validação ----------

    [Theory]
    [InlineData("", 10, 1)]          // nome vazio
    [InlineData("A", 10, 1)]         // nome curto
    [InlineData("Valido", -1, 1)]    // preço negativo
    [InlineData("Valido", 10, -5)]   // quantidade negativa
    public async Task Criar_com_dados_invalidos_retorna_400(string nome, decimal preco, int qtd)
    {
        var r = await _c.PostAsJsonAsync("/api/produtos", new { nome, preco, quantidade = qtd, estoqueMinimo = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("errors", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Nome_muito_longo_retorna_400()
    {
        var r = await _c.PostAsJsonAsync("/api/produtos", Valido(new string('x', 200)));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Json_malformado_retorna_400_sem_stack_trace()
    {
        var r = await _c.PostAsync("/api/produtos", new StringContent("{ nome: ", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var corpo = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("   at ", corpo);
        Assert.DoesNotContain("Exception", corpo);
    }

    [Fact]
    public async Task Cliente_nao_consegue_definir_Id()
    {
        var r = await _c.PostAsJsonAsync("/api/produtos", new { id = 999999, nome = "Tentativa Id", preco = 1, quantidade = 1, estoqueMinimo = 0 });
        var p = await r.Content.ReadFromJsonAsync<Produto>();
        Assert.NotEqual(999999, p!.Id);
    }

    [Fact]
    public async Task Busca_muito_longa_retorna_400()
    {
        var r = await _c.GetAsync("/api/produtos?busca=" + new string('a', 101));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Id_invalido_retorna_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _c.GetAsync("/api/produtos/0")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _c.GetAsync("/api/produtos/-1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _c.GetAsync("/api/produtos/abc")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _c.DeleteAsync("/api/produtos/987654")).StatusCode);
    }

    // ---------- Movimentação ----------

    [Fact]
    public async Task Movimentar_soma_e_subtrai()
    {
        var p = await Criar("Movimento");
        var r = await _c.PatchAsync($"/api/produtos/{p.Id}/movimentar?delta=3", null);
        Assert.Equal(8, (await r.Content.ReadFromJsonAsync<Produto>())!.Quantidade);

        r = await _c.PatchAsync($"/api/produtos/{p.Id}/movimentar?delta=-8", null);
        Assert.Equal(0, (await r.Content.ReadFromJsonAsync<Produto>())!.Quantidade);
    }

    [Fact]
    public async Task Movimentar_abaixo_de_zero_retorna_409()
    {
        var p = await Criar("Sem Saldo");
        var r = await _c.PatchAsync($"/api/produtos/{p.Id}/movimentar?delta=-6", null);
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    [InlineData(int.MaxValue)]
    public async Task Movimentar_delta_invalido_retorna_400(int delta)
    {
        var p = await Criar("Delta Invalido");
        var r = await _c.PatchAsync($"/api/produtos/{p.Id}/movimentar?delta={delta}", null);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Movimentacoes_simultaneas_nao_perdem_atualizacoes()
    {
        var p = await Criar("Concorrencia");
        var tarefas = Enumerable.Range(0, 20).Select(_ => _c.PatchAsync($"/api/produtos/{p.Id}/movimentar?delta=1", null));
        await Task.WhenAll(tarefas);
        var final = await _c.GetFromJsonAsync<Produto>($"/api/produtos/{p.Id}");
        Assert.Equal(25, final!.Quantidade);
    }

    // ---------- Segurança ----------

    [Fact]
    public async Task Respostas_tem_cabecalhos_de_seguranca()
    {
        var r = await _c.GetAsync("/api/produtos");
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(r.Headers.Contains("Content-Security-Policy"));
        Assert.False(r.Headers.Contains("Server"));
    }

    [Fact]
    public async Task Tentativa_de_sql_injection_nao_afeta_dados()
    {
        var antes = (await _c.GetFromJsonAsync<List<Produto>>("/api/produtos"))!.Count;
        var r = await _c.GetAsync("/api/produtos?busca=" + Uri.EscapeDataString("'; DROP TABLE Produtos; --"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var depois = (await _c.GetFromJsonAsync<List<Produto>>("/api/produtos"))!.Count;
        Assert.Equal(antes, depois);
    }

    [Fact]
    public async Task Swagger_nao_exposto_fora_de_desenvolvimento()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _c.GetAsync("/swagger/index.html")).StatusCode);
    }

    [Fact]
    public async Task Corpo_acima_do_limite_e_rejeitado()
    {
        var grande = new StringContent("{\"nome\":\"" + new string('x', 64 * 1024) + "\"}", Encoding.UTF8, "application/json");
        var r = await _c.PostAsync("/api/produtos", grande);
        Assert.True(r.StatusCode is HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cors_bloqueia_origem_desconhecida()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/produtos");
        req.Headers.Add("Origin", "https://site-malicioso.com");
        var r = await _c.SendAsync(req);
        Assert.False(r.Headers.Contains("Access-Control-Allow-Origin"));
    }
}

public class RateLimitTests
{
    [Fact]
    public async Task Excesso_de_requisicoes_retorna_429()
    {
        using var f = new ApiFactory { RateLimit = 5 };
        var c = await f.ClienteLogado(ApiFactory.Admin);
        HttpStatusCode ultimo = HttpStatusCode.OK;
        for (var i = 0; i < 8; i++) ultimo = (await c.GetAsync("/api/produtos")).StatusCode;
        Assert.Equal(HttpStatusCode.TooManyRequests, ultimo);
    }
}
