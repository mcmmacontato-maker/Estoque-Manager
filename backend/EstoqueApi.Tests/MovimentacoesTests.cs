using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EstoqueApi.Models;

namespace EstoqueApi.Tests;

public class MovimentacoesTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly ApiFactory _f;
    private HttpClient _c = null!;
    public MovimentacoesTests(ApiFactory f) => _f = f;

    public async Task InitializeAsync() => _c = await _f.ClienteLogado(ApiFactory.Admin);
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Produto> Criar(string nome, int qtd = 10)
    {
        var r = await _c.PostAsJsonAsync("/api/produtos", new { nome, categoria = "Hist", preco = 2m, quantidade = qtd, estoqueMinimo = 1 });
        return (await r.Content.ReadFromJsonAsync<Produto>())!;
    }

    private Task<List<Movimentacao>> Historico(int produtoId) =>
        _c.GetFromJsonAsync<List<Movimentacao>>($"/api/movimentacoes?produtoId={produtoId}")!;

    [Fact]
    public async Task Cadastro_entrada_saida_e_ajuste_ficam_registrados()
    {
        var p = await Criar("Rastreado", 10);
        await _c.PatchAsync($"/api/produtos/{p.Id}/movimentar?delta=5", null);
        await _c.PatchAsync($"/api/produtos/{p.Id}/movimentar?delta=-3", null);
        await _c.PutAsJsonAsync($"/api/produtos/{p.Id}", new { nome = "Rastreado", categoria = "Hist", preco = 2m, quantidade = 20, estoqueMinimo = 1 });

        var h = await Historico(p.Id); // mais recente primeiro
        Assert.Equal(["Ajuste", "Saída", "Entrada", "Cadastro"], h.Select(m => m.Tipo));
        Assert.Equal([8, -3, 5, 10], h.Select(m => m.Quantidade));
        Assert.Equal([20, 12, 15, 10], h.Select(m => m.SaldoApos));
        Assert.All(h, m => Assert.Equal("Admin", m.UsuarioNome));
    }

    [Fact]
    public async Task Movimentacao_recusada_nao_gera_registro()
    {
        var p = await Criar("Sem saldo", 1);
        var r = await _c.PatchAsync($"/api/produtos/{p.Id}/movimentar?delta=-5", null);
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Single(await Historico(p.Id)); // só o cadastro
    }

    [Fact]
    public async Task Edicao_sem_mudar_quantidade_nao_gera_registro()
    {
        var p = await Criar("Só nome", 4);
        await _c.PutAsJsonAsync($"/api/produtos/{p.Id}", new { nome = "Só nome editado", categoria = "Hist", preco = 3m, quantidade = 4, estoqueMinimo = 1 });
        Assert.Single(await Historico(p.Id));
    }

    [Fact]
    public async Task Historico_continua_apos_excluir_produto()
    {
        var p = await Criar("Será excluído", 2);
        await _c.DeleteAsync($"/api/produtos/{p.Id}");
        var h = await Historico(p.Id);
        Assert.Equal("Será excluído", h.Single().ProdutoNome);
    }

    [Fact]
    public async Task Filtro_por_tipo()
    {
        var p = await Criar("Filtro tipo", 10);
        await _c.PatchAsync($"/api/produtos/{p.Id}/movimentar?delta=-1", null);
        var saidas = await _c.GetFromJsonAsync<List<Movimentacao>>($"/api/movimentacoes?produtoId={p.Id}&tipo=Sa%C3%ADda");
        Assert.Single(saidas!);
        Assert.Equal(HttpStatusCode.BadRequest, (await _c.GetAsync("/api/movimentacoes?tipo=Hack")).StatusCode);
    }

    [Fact]
    public async Task Datas_sao_retornadas_em_utc()
    {
        var p = await Criar("Fuso", 1);
        var json = await _c.GetStringAsync($"/api/movimentacoes?produtoId={p.Id}");
        var data = JsonDocument.Parse(json).RootElement[0].GetProperty("data").GetString();
        Assert.EndsWith("Z", data);
    }

    [Fact]
    public async Task Painel_retorna_indicadores_coerentes()
    {
        var produtos = await _c.GetFromJsonAsync<List<Produto>>("/api/produtos");
        var painel = JsonDocument.Parse(await _c.GetStringAsync("/api/painel")).RootElement;

        var totais = painel.GetProperty("totais");
        Assert.Equal(produtos!.Count, totais.GetProperty("produtos").GetInt32());
        Assert.Equal(produtos.Sum(p => p.Quantidade), totais.GetProperty("unidades").GetInt32());
        Assert.Equal(14, painel.GetProperty("serie").GetArrayLength());
        Assert.True(painel.GetProperty("porCategoria").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Operador_ve_painel_e_historico()
    {
        var c = await _f.ClienteLogado(ApiFactory.Operador);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/painel")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/movimentacoes")).StatusCode);
    }

    [Fact]
    public async Task Sem_login_painel_e_historico_retornam_401()
    {
        var c = _f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/painel")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/movimentacoes")).StatusCode);
    }
}
