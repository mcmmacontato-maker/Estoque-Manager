using System.Net;
using System.Net.Http.Json;
using EstoqueApi.Data;
using EstoqueApi.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EstoqueApi.Tests;

public class DemoFactory : ApiFactory
{
    public DemoFactory() { Demo = true; }
}

public class DemoTests : IClassFixture<DemoFactory>
{
    private readonly DemoFactory _f;
    public DemoTests(DemoFactory f) => _f = f;

    [Fact]
    public async Task Info_indica_demo_ativo()
    {
        var info = await _f.CreateClient().GetFromJsonAsync<Dictionary<string, object>>("/api/demo");
        Assert.Equal("True", info!["ativo"].ToString(), ignoreCase: true);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Operador")]
    public async Task Login_rapido_por_perfil(string perfil)
    {
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/demo", new { perfil });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(perfil, (await r.Content.ReadFromJsonAsync<UsuarioDto>())!.Perfil);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/produtos")).StatusCode);
    }

    [Fact]
    public async Task Login_rapido_respeita_permissoes_do_perfil()
    {
        var c = _f.CreateClient();
        await c.PostAsJsonAsync("/api/auth/demo", new { perfil = "Operador" });
        Assert.Equal(HttpStatusCode.Forbidden, (await c.DeleteAsync("/api/produtos/1")).StatusCode);
    }

    [Fact]
    public async Task Perfil_invalido_retorna_400()
    {
        var r = await _f.CreateClient().PostAsJsonAsync("/api/auth/demo", new { perfil = "SuperUser" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Restaurar_volta_ao_catalogo_inicial()
    {
        var c = await _f.ClienteLogado(ApiFactory.Admin);
        await c.DeleteAsync("/api/produtos/1");
        await c.PostAsJsonAsync("/api/produtos", new { nome = "Lixo de visitante", preco = 1, quantidade = 1, estoqueMinimo = 0 });

        using (var scope = _f.Services.CreateScope())
            await EstoqueApi.Infra.Demo.RestaurarAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        var lista = await c.GetFromJsonAsync<List<Produto>>("/api/produtos");
        Assert.Equal(AppDbContext.ProdutosIniciais().Length, lista!.Count);
        Assert.DoesNotContain(lista, p => p.Nome == "Lixo de visitante");
        Assert.Contains(lista, p => p.Id == 1);
    }
}

public class DemoDesligadoTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;
    public DemoDesligadoTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Login_rapido_nao_existe_fora_do_modo_demo()
    {
        var r = await _f.CreateClient().PostAsJsonAsync("/api/auth/demo", new { perfil = "Admin" });
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task Info_indica_demo_inativo()
    {
        var info = await _f.CreateClient().GetFromJsonAsync<Dictionary<string, object>>("/api/demo");
        Assert.Equal("False", info!["ativo"].ToString(), ignoreCase: true);
    }
}
