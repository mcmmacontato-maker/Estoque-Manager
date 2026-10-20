using EstoqueApi.Data;
using EstoqueApi.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=estoque.db"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();

var api = app.MapGroup("/api/produtos");

api.MapGet("/", async (AppDbContext db, string? busca) =>
{
    var q = db.Produtos.AsQueryable();
    if (!string.IsNullOrWhiteSpace(busca))
        q = q.Where(p => p.Nome.ToLower().Contains(busca.ToLower()) || p.Categoria.ToLower().Contains(busca.ToLower()));
    return await q.OrderBy(p => p.Nome).ToListAsync();
});

api.MapGet("/{id:int}", async (int id, AppDbContext db) =>
    await db.Produtos.FindAsync(id) is { } p ? Results.Ok(p) : Results.NotFound());

api.MapPost("/", async (Produto p, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(p.Nome)) return Results.BadRequest("Nome é obrigatório.");
    p.Id = 0;
    p.AtualizadoEm = DateTime.UtcNow;
    db.Produtos.Add(p);
    await db.SaveChangesAsync();
    return Results.Created($"/api/produtos/{p.Id}", p);
});

api.MapPut("/{id:int}", async (int id, Produto input, AppDbContext db) =>
{
    var p = await db.Produtos.FindAsync(id);
    if (p is null) return Results.NotFound();
    p.Nome = input.Nome;
    p.Categoria = input.Categoria;
    p.Preco = input.Preco;
    p.Quantidade = input.Quantidade;
    p.EstoqueMinimo = input.EstoqueMinimo;
    p.AtualizadoEm = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(p);
});

api.MapPatch("/{id:int}/movimentar", async (int id, int delta, AppDbContext db) =>
{
    var p = await db.Produtos.FindAsync(id);
    if (p is null) return Results.NotFound();
    if (p.Quantidade + delta < 0) return Results.BadRequest("Estoque insuficiente.");
    p.Quantidade += delta;
    p.AtualizadoEm = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(p);
});

api.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
{
    var p = await db.Produtos.FindAsync(id);
    if (p is null) return Results.NotFound();
    db.Produtos.Remove(p);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run("http://localhost:5000");
