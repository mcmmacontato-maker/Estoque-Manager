using System.Net;
using System.Net.Http.Json;
using EstoqueApi.Models;

namespace EstoqueApi.Tests;

public class AuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;
    public AuthTests(ApiFactory f) => _f = f;

    private static object Login(string email, string senha) => new { email, senha };
    private static object Produto() => new { nome = "Produto Auth", preco = 1, quantidade = 1, estoqueMinimo = 0 };

    [Theory]
    [InlineData("GET", "/api/produtos")]
    [InlineData("GET", "/api/produtos/1")]
    [InlineData("POST", "/api/produtos")]
    [InlineData("PUT", "/api/produtos/1")]
    [InlineData("PATCH", "/api/produtos/1/movimentar?delta=1")]
    [InlineData("DELETE", "/api/produtos/1")]
    [InlineData("GET", "/api/auth/me")]
    public async Task Sem_login_retorna_401(string metodo, string url)
    {
        var req = new HttpRequestMessage(new HttpMethod(metodo), url);
        if (metodo is "POST" or "PUT") req.Content = JsonContent.Create(Produto());
        var r = await _f.CreateClient().SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Login_valido_retorna_usuario_e_cookie_seguro()
    {
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", Login(ApiFactory.Admin, ApiFactory.Senha));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        var u = await r.Content.ReadFromJsonAsync<UsuarioDto>();
        Assert.Equal("Admin", u!.Perfil);

        var cookie = r.Headers.GetValues("Set-Cookie").Single(s => s.StartsWith("estoque.auth="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);

        var me = await c.GetFromJsonAsync<UsuarioDto>("/api/auth/me");
        Assert.Equal(ApiFactory.Admin, me!.Email);
    }

    [Fact]
    public async Task Resposta_de_login_nao_expoe_hash_da_senha()
    {
        var r = await _f.CreateClient().PostAsJsonAsync("/api/auth/login", Login(ApiFactory.Admin, ApiFactory.Senha));
        var corpo = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("senhaHash", corpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("carimbo", corpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Email_com_maiusculas_e_espacos_funciona()
    {
        var r = await _f.CreateClient().PostAsJsonAsync("/api/auth/login", Login("  ADMIN@Teste.Local ", ApiFactory.Senha));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task Senha_errada_e_email_inexistente_tem_mesma_resposta()
    {
        var c = _f.CreateClient();
        var r1 = await c.PostAsJsonAsync("/api/auth/login", Login(ApiFactory.Operador, "errada123"));
        var r2 = await c.PostAsJsonAsync("/api/auth/login", Login("naoexiste@teste.local", "errada123"));
        Assert.Equal(HttpStatusCode.Unauthorized, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, r2.StatusCode);
        Assert.Equal(
            (await r1.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["detail"].ToString(),
            (await r2.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["detail"].ToString());
    }

    [Fact]
    public async Task Login_com_dados_invalidos_retorna_400()
    {
        var r = await _f.CreateClient().PostAsJsonAsync("/api/auth/login", Login("nao-e-email", ""));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Conta_bloqueia_apos_5_falhas_mesmo_com_senha_correta()
    {
        var c = _f.CreateClient();
        for (var i = 0; i < AuthEndpoints_MaxFalhas; i++)
            await c.PostAsJsonAsync("/api/auth/login", Login(ApiFactory.Vitima, "errada123"));

        var r = await c.PostAsJsonAsync("/api/auth/login", Login(ApiFactory.Vitima, ApiFactory.Senha));
        Assert.Equal((HttpStatusCode)423, r.StatusCode);
    }
    private const int AuthEndpoints_MaxFalhas = 5;

    [Fact]
    public async Task Logout_encerra_sessao()
    {
        var c = await _f.ClienteLogado(ApiFactory.Operador);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/produtos")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/produtos")).StatusCode);
    }

    [Fact]
    public async Task Cookie_forjado_e_rejeitado()
    {
        var c = _f.CreateClient();
        c.DefaultRequestHeaders.Add("Cookie", "estoque.auth=CfDJ8forjado1234567890");
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/produtos")).StatusCode);
    }

    // ---------- Perfis ----------

    [Fact]
    public async Task Operador_pode_ler_e_movimentar()
    {
        var c = await _f.ClienteLogado(ApiFactory.Operador);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/produtos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PatchAsync("/api/produtos/1/movimentar?delta=1", null)).StatusCode);
    }

    [Fact]
    public async Task Operador_nao_pode_criar_editar_ou_excluir()
    {
        var c = await _f.ClienteLogado(ApiFactory.Operador);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/produtos", Produto())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync("/api/produtos/1", Produto())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.DeleteAsync("/api/produtos/1")).StatusCode);
    }

    [Fact]
    public async Task Excesso_de_tentativas_de_login_retorna_429()
    {
        using var f = new ApiFactory { LoginRateLimit = 3 };
        var c = f.CreateClient();
        HttpStatusCode ultimo = HttpStatusCode.OK;
        for (var i = 0; i < 5; i++)
            ultimo = (await c.PostAsJsonAsync("/api/auth/login", Login("x@teste.local", "qualquer1"))).StatusCode;
        Assert.Equal(HttpStatusCode.TooManyRequests, ultimo);
    }
}
