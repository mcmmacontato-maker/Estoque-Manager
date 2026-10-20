using System.Security.Claims;
using EstoqueApi.Data;
using EstoqueApi.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EstoqueApi.Infra;

public static class AuthEndpoints
{
    public const int MaxFalhas = 5;
    public static readonly TimeSpan TempoBloqueio = TimeSpan.FromMinutes(15);
    public const string ClaimCarimbo = "carimbo";

    public static void MapAuth(this WebApplication app)
    {
        var g = app.MapGroup("/api/auth").WithTags("Autenticação");

        g.MapPost("/login", Login)
            .AllowAnonymous()
            .RequireRateLimiting("login")
            .AddEndpointFilter<ValidacaoFilter<LoginInput>>();

        g.MapPost("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization();

        g.MapGet("/me", async (ClaimsPrincipal user, AppDbContext db) =>
        {
            var id = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var u = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return u is null ? Results.Unauthorized() : Results.Ok(UsuarioDto.De(u));
        }).RequireAuthorization().RequireRateLimiting("api");
    }

    private static async Task<IResult> Login(LoginInput input, HttpContext ctx, AppDbContext db, IPasswordHasher<Usuario> hasher, ILogger<Program> log)
    {
        var email = input.Email.Trim().ToLowerInvariant();
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Email == email);
        var ip = ctx.Connection.RemoteIpAddress?.ToString();
        var agora = DateTime.UtcNow;

        // Mensagem única para não revelar se o e-mail existe (evita enumeração de usuários)
        var falha = Results.Problem("E-mail ou senha inválidos.", statusCode: StatusCodes.Status401Unauthorized);

        if (u is null || !u.Ativo)
        {
            // Executa um hash mesmo assim para igualar o tempo de resposta
            hasher.VerifyHashedPassword(new Usuario(), DummyHash.Valor, input.Senha);
            log.LogWarning("Login falhou para {Email} (inexistente/inativo) - IP {Ip}", email, ip);
            return falha;
        }

        if (u.BloqueadoAte > agora)
        {
            log.LogWarning("Login bloqueado para {Email} - IP {Ip}", email, ip);
            return Results.Problem("Conta temporariamente bloqueada por excesso de tentativas. Tente novamente mais tarde.",
                statusCode: StatusCodes.Status423Locked);
        }

        var resultado = hasher.VerifyHashedPassword(u, u.SenhaHash, input.Senha);
        if (resultado == PasswordVerificationResult.Failed)
        {
            u.FalhasLogin++;
            if (u.FalhasLogin >= MaxFalhas)
            {
                u.BloqueadoAte = agora.Add(TempoBloqueio);
                u.FalhasLogin = 0;
                log.LogWarning("Conta {Email} bloqueada por {Min} min após {N} falhas - IP {Ip}", email, TempoBloqueio.TotalMinutes, MaxFalhas, ip);
            }
            await db.SaveChangesAsync();
            return falha;
        }

        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
            u.SenhaHash = hasher.HashPassword(u, input.Senha);

        u.FalhasLogin = 0;
        u.BloqueadoAte = null;
        await db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.Nome),
            new(ClaimTypes.Email, u.Email),
            new(ClaimTypes.Role, u.Perfil),
            new(ClaimCarimbo, u.CarimboSeguranca)
        };
        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

        log.LogInformation("Login de {Email} ({Perfil}) - IP {Ip}", email, u.Perfil, ip);
        return Results.Ok(UsuarioDto.De(u));
    }

    /// <summary>
    /// A cada requisição autenticada, confere se o usuário ainda existe, está ativo e com o mesmo
    /// carimbo de segurança. Assim, desativar o usuário ou trocar a senha derruba as sessões.
    /// </summary>
    public static async Task ValidarSessao(CookieValidatePrincipalContext ctx)
    {
        var p = ctx.Principal;
        if (!int.TryParse(p?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) { ctx.RejectPrincipal(); return; }

        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var u = await db.Usuarios.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Ativo, x.CarimboSeguranca, x.Perfil })
            .FirstOrDefaultAsync();

        if (u is null || !u.Ativo || u.CarimboSeguranca != p!.FindFirstValue(ClaimCarimbo) || u.Perfil != p.FindFirstValue(ClaimTypes.Role))
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    private static class DummyHash
    {
        public static readonly string Valor = new PasswordHasher<Usuario>().HashPassword(new Usuario(), Guid.NewGuid().ToString());
    }
}
