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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("RateLimit:PorMinuto", RateLimit.ToString());
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<DbContextOptions<AppDbContext>>();
            s.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={_db}"));
        });
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
