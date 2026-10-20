using EstoqueApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EstoqueApi.Tests;

/// <summary>Sobe a API em memória com um banco SQLite isolado por instância.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"estoque-test-{Guid.NewGuid():N}.db");

    public int RateLimit { get; init; } = 10_000;
    public int LoginRateLimit { get; init; } = 10_000;

    public const string Senha = "Senha@Teste1";
    public const string Admin = "admin@teste.local";
    public const string Operador = "operador@teste.local";
    public const string Vitima = "bloqueio@teste.local";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("RateLimit:PorMinuto", RateLimit.ToString());
        builder.UseSetting("RateLimit:LoginPorMinuto", LoginRateLimit.ToString());
        var usuarios = new[] { (Admin, "Admin"), (Operador, "Operador"), (Vitima, "Operador") };
        for (var i = 0; i < usuarios.Length; i++)
        {
            builder.UseSetting($"Seed:Usuarios:{i}:Email", usuarios[i].Item1);
            builder.UseSetting($"Seed:Usuarios:{i}:Nome", usuarios[i].Item2);
            builder.UseSetting($"Seed:Usuarios:{i}:Senha", Senha);
            builder.UseSetting($"Seed:Usuarios:{i}:Perfil", usuarios[i].Item2);
        }
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<DbContextOptions<AppDbContext>>();
            s.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={_db}"));
        });
    }

    /// <summary>Cria um cliente já autenticado (o HttpClient da fábrica guarda cookies).</summary>
    public async Task<HttpClient> ClienteLogado(string email)
    {
        var c = CreateClient();
        var r = await c.PostAsync("/api/auth/login", System.Net.Http.Json.JsonContent.Create(new { email, senha = Senha }));
        r.EnsureSuccessStatusCode();
        return c;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_db); } catch { /* arquivo temporário */ }
    }
}

internal static class ServiceCollectionExtensions
{
    public static void RemoveAll<T>(this IServiceCollection s)
    {
        foreach (var d in s.Where(d => d.ServiceType == typeof(T)).ToList()) s.Remove(d);
    }
}
