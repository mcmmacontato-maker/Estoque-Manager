using System.Threading.RateLimiting;
using EstoqueApi.Data;
using EstoqueApi.Infra;
using EstoqueApi.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Limita o tamanho do corpo das requisições (a API só recebe JSON pequeno)
builder.WebHost.ConfigureKestrel(k =>
{
    k.Limits.MaxRequestBodySize = 32 * 1024;
    k.AddServerHeader = false; // não expõe "Server: Kestrel"
});

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=estoque.db"));

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var origens = builder.Configuration.GetSection("Cors:Origens").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(origens).WithHeaders("Content-Type").WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")));

// Rate limiting por IP: protege contra abuso/força bruta
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("api", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimit:PorMinuto", 120),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

// Erros não tratados viram ProblemDetails genérico, sem stack trace para o cliente
app.UseExceptionHandler(e => e.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var status = ex is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError;
    if (status == 500) app.Logger.LogError(ex, "Erro não tratado");
    await Results.Problem(
        title: status == 500 ? "Erro interno no servidor." : "Requisição inválida.",
        statusCode: status).ExecuteAsync(ctx);
}));
app.UseStatusCodePages();

// Cabeçalhos de segurança
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h.XContentTypeOptions = "nosniff";
    h.XFrameOptions = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    h["Cross-Origin-Resource-Policy"] = "same-site";
    if (!ctx.Request.Path.StartsWithSegments("/swagger"))
        h.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
    await next();
});

if (app.Environment.IsDevelopment())
{
    // Documentação só em desenvolvimento
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseCors();
app.UseRateLimiter();

var api = app.MapGroup("/api/produtos").RequireRateLimiting("api").WithTags("Produtos");

api.MapGet("/", async (AppDbContext db, string? busca) =>
{
    if (busca?.Length > 100) return Results.ValidationProblem(new Dictionary<string, string[]> { ["busca"] = ["Máximo de 100 caracteres."] });

    var q = db.Produtos.AsNoTracking();
    if (!string.IsNullOrWhiteSpace(busca))
    {
        // EF Core parametriza a consulta: sem risco de SQL injection
        var termo = busca.Trim().ToLower();
        q = q.Where(p => p.Nome.ToLower().Contains(termo) || p.Categoria.ToLower().Contains(termo));
    }
    return Results.Ok(await q.OrderBy(p => p.Nome).Take(500).ToListAsync());
});

api.MapGet("/{id:int:min(1)}", async (int id, AppDbContext db) =>
    await db.Produtos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id) is { } p ? Results.Ok(p) : Results.NotFound());

api.MapPost("/", async (ProdutoInput input, AppDbContext db) =>
{
    var p = new Produto();
    input.AplicarEm(p);
    db.Produtos.Add(p);
    await db.SaveChangesAsync();
    return Results.Created($"/api/produtos/{p.Id}", p);
}).AddEndpointFilter<ValidacaoFilter<ProdutoInput>>();

api.MapPut("/{id:int:min(1)}", async (int id, ProdutoInput input, AppDbContext db) =>
{
    var p = await db.Produtos.FindAsync(id);
    if (p is null) return Results.NotFound();
    input.AplicarEm(p);
    await db.SaveChangesAsync();
    return Results.Ok(p);
}).AddEndpointFilter<ValidacaoFilter<ProdutoInput>>();

api.MapPatch("/{id:int:min(1)}/movimentar", async (int id, int delta, AppDbContext db) =>
{
    if (delta == 0 || Math.Abs(delta) > 10_000)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["delta"] = ["Delta deve ser diferente de zero e no máximo 10.000 em valor absoluto."] });

    // Atualização atômica: evita condição de corrida entre requisições simultâneas
    var afetados = await db.Produtos
        .Where(p => p.Id == id && p.Quantidade + delta >= 0 && p.Quantidade + delta <= 1_000_000)
        .ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Quantidade, p => p.Quantidade + delta)
            .SetProperty(p => p.AtualizadoEm, DateTime.UtcNow));

    if (afetados == 0)
    {
        var existe = await db.Produtos.AnyAsync(p => p.Id == id);
        return existe
            ? Results.Problem("Estoque insuficiente ou limite excedido.", statusCode: StatusCodes.Status409Conflict)
            : Results.NotFound();
    }
    return Results.Ok(await db.Produtos.AsNoTracking().FirstAsync(p => p.Id == id));
});

api.MapDelete("/{id:int:min(1)}", async (int id, AppDbContext db) =>
    await db.Produtos.Where(p => p.Id == id).ExecuteDeleteAsync() > 0 ? Results.NoContent() : Results.NotFound());

app.Run();

public partial class Program; // exposto para os testes de integração
