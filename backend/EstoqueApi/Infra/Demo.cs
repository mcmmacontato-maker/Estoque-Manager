using System.Security.Claims;
using EstoqueApi.Data;
using EstoqueApi.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace EstoqueApi.Infra;

public class DemoOptions
{
    /// <summary>Habilita login com um clique e o reset periódico dos dados.</summary>
    public bool Ativo { get; set; }

    /// <summary>Intervalo do reset automático. 0 desliga o reset.</summary>
    public int ResetMinutos { get; set; } = 60;
}

public record DemoLoginInput(string Perfil);

/// <summary>
/// Modo demonstração (portfólio): permite entrar como Admin ou Operador sem digitar senha
/// e restaura os dados periodicamente. Desligado por padrão; só existe se Demo:Ativo = true.
/// </summary>
public static class Demo
{
    public static DateTime? ProximoReset { get; set; }

    public static void MapDemo(this WebApplication app)
    {
        var cfg = app.Configuration.GetSection("Demo").Get<DemoOptions>() ?? new();

        app.MapGet("/api/demo", () => Results.Ok(new
        {
            ativo = cfg.Ativo,
            resetMinutos = cfg.Ativo ? cfg.ResetMinutos : 0,
            proximoReset = cfg.Ativo ? ProximoReset : null
        })).AllowAnonymous().RequireRateLimiting("api").WithTags("Demonstração");

        if (!cfg.Ativo) return; // fora do modo demo o endpoint de login rápido nem é registrado

        app.MapPost("/api/auth/demo", async (DemoLoginInput input, HttpContext ctx, AppDbContext db, ILogger<Program> log) =>
        {
            if (!Perfis.Todos.Contains(input.Perfil))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["perfil"] = ["Perfil inválido."] });

            var u = await db.Usuarios.AsNoTracking()
                .Where(x => x.Perfil == input.Perfil && x.Ativo)
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync();
            if (u is null)
                return Results.Problem("Conta de demonstração indisponível.", statusCode: StatusCodes.Status404NotFound);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, u.Id.ToString()),
                new(ClaimTypes.Name, u.Nome),
                new(ClaimTypes.Email, u.Email),
                new(ClaimTypes.Role, u.Perfil),
                new(AuthEndpoints.ClaimCarimbo, u.CarimboSeguranca)
            };
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

            log.LogInformation("Login de demonstração como {Perfil} - IP {Ip}", u.Perfil, ctx.Connection.RemoteIpAddress);
            return Results.Ok(UsuarioDto.De(u));
        }).AllowAnonymous().RequireRateLimiting("login").WithTags("Demonstração");
    }

    /// <summary>Restaura o catálogo inicial e desbloqueia contas.</summary>
    public static async Task RestaurarAsync(AppDbContext db, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Produtos.ExecuteDeleteAsync(ct);
        db.Produtos.AddRange(AppDbContext.ProdutosIniciais());
        await db.SaveChangesAsync(ct);
        await db.Usuarios.ExecuteUpdateAsync(s => s
            .SetProperty(u => u.FalhasLogin, 0)
            .SetProperty(u => u.BloqueadoAte, (DateTime?)null), ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }
}

/// <summary>Executa o reset do modo demonstração no intervalo configurado.</summary>
public class DemoResetService(IServiceScopeFactory scopes, IConfiguration cfg, ILogger<DemoResetService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var opt = cfg.GetSection("Demo").Get<DemoOptions>() ?? new();
        if (!opt.Ativo || opt.ResetMinutos <= 0) return;

        var intervalo = TimeSpan.FromMinutes(opt.ResetMinutos);
        Demo.ProximoReset = DateTime.UtcNow.Add(intervalo);
        using var timer = new PeriodicTimer(intervalo);

        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await Demo.RestaurarAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), ct);
                log.LogInformation("Dados da demonstração restaurados.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Falha ao restaurar dados da demonstração.");
            }
            Demo.ProximoReset = DateTime.UtcNow.Add(intervalo);
        }
    }
}
