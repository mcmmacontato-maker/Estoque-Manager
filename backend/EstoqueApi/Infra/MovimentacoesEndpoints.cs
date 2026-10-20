using System.Security.Claims;
using EstoqueApi.Data;
using EstoqueApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EstoqueApi.Infra;

public static class MovimentacoesEndpoints
{
    public static Movimentacao Registro(Produto p, string tipo, int delta, ClaimsPrincipal user) => new()
    {
        ProdutoId = p.Id,
        ProdutoNome = p.Nome,
        Tipo = tipo,
        Quantidade = delta,
        SaldoApos = p.Quantidade,
        UsuarioNome = user.FindFirstValue(ClaimTypes.Name) ?? "Sistema",
        Data = DateTime.UtcNow
    };

    public static void MapMovimentacoes(this WebApplication app)
    {
        app.MapGet("/api/movimentacoes", async (AppDbContext db, int? produtoId, string? tipo, int? dias) =>
        {
            if (tipo is not null && !TiposMovimentacao.Todos.Contains(tipo))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["tipo"] = ["Tipo inválido."] });

            var q = db.Movimentacoes.AsNoTracking();
            if (produtoId is > 0) q = q.Where(m => m.ProdutoId == produtoId);
            if (tipo is not null) q = q.Where(m => m.Tipo == tipo);
            if (dias is > 0) q = q.Where(m => m.Data >= DateTime.UtcNow.AddDays(-Math.Min(dias.Value, 365)));

            return Results.Ok(await q.OrderByDescending(m => m.Data).ThenByDescending(m => m.Id).Take(300).ToListAsync());
        }).RequireAuthorization().RequireRateLimiting("api").WithTags("Movimentações");

        app.MapGet("/api/painel", async (AppDbContext db) =>
        {
            // Volume pequeno (≤ centenas de itens): agregar em memória mantém a consulta simples e portátil
            var produtos = await db.Produtos.AsNoTracking().ToListAsync();
            var inicio = DateTime.UtcNow.Date.AddDays(-13);
            var movs = await db.Movimentacoes.AsNoTracking().Where(m => m.Data >= inicio).ToListAsync();

            var serie = Enumerable.Range(0, 14).Select(i =>
            {
                var dia = inicio.AddDays(i);
                var doDia = movs.Where(m => m.Data.Date == dia).ToList();
                return new
                {
                    dia,
                    entradas = doDia.Where(m => m.Quantidade > 0).Sum(m => m.Quantidade),
                    saidas = doDia.Where(m => m.Quantidade < 0).Sum(m => -m.Quantidade)
                };
            }).ToList();

            return Results.Ok(new
            {
                totais = new
                {
                    produtos = produtos.Count,
                    unidades = produtos.Sum(p => p.Quantidade),
                    valor = produtos.Sum(p => p.Preco * p.Quantidade),
                    esgotados = produtos.Count(p => p.Quantidade == 0),
                    baixo = produtos.Count(p => p.Quantidade > 0 && p.Quantidade <= p.EstoqueMinimo),
                    entradas14d = serie.Sum(s => s.entradas),
                    saidas14d = serie.Sum(s => s.saidas)
                },
                porCategoria = produtos
                    .GroupBy(p => p.Categoria)
                    .Select(g => new { categoria = g.Key, valor = g.Sum(p => p.Preco * p.Quantidade), unidades = g.Sum(p => p.Quantidade), itens = g.Count() })
                    .OrderByDescending(c => c.valor)
                    .ToList(),
                criticos = produtos
                    .Where(p => p.Quantidade <= p.EstoqueMinimo)
                    .OrderBy(p => p.Quantidade).ThenBy(p => p.Quantidade - p.EstoqueMinimo)
                    .Take(6)
                    .Select(p => new { p.Id, p.Nome, p.Categoria, p.Quantidade, p.EstoqueMinimo })
                    .ToList(),
                serie,
                ultimas = await db.Movimentacoes.AsNoTracking().OrderByDescending(m => m.Data).ThenByDescending(m => m.Id).Take(6).ToListAsync()
            });
        }).RequireAuthorization().RequireRateLimiting("api").WithTags("Painel");
    }
}
