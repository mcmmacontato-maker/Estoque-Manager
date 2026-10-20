using EstoqueApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EstoqueApi.Data;

/// <summary>
/// Gera um histórico fictício e coerente para o modo demonstração: as movimentações são criadas
/// "de trás para frente" a partir do saldo atual, então o saldo após cada uma sempre bate.
/// </summary>
public static class SeedMovimentacoes
{
    public static async Task GerarAsync(AppDbContext db, int dias = 21, CancellationToken ct = default)
    {
        var produtos = await db.Produtos.AsNoTracking().ToListAsync(ct);
        var nomes = await db.Usuarios.AsNoTracking().OrderBy(u => u.Id).Select(u => u.Nome).ToListAsync(ct);
        if (produtos.Count == 0) return;
        if (nomes.Count == 0) nomes.Add("Sistema");

        var rnd = new Random(2026); // determinístico: a demo fica sempre igual
        var agora = DateTime.UtcNow;
        var lista = new List<Movimentacao>();

        foreach (var p in produtos)
        {
            var saldo = p.Quantidade;
            var quando = agora.AddHours(-rnd.Next(2, 30));
            var qtdMovs = rnd.Next(3, 7);

            for (var i = 0; i < qtdMovs && quando > agora.AddDays(-dias); i++)
            {
                // Entrada só é possível se o saldo atual comportar (saldo anterior não pode ser negativo)
                var entrada = saldo >= 4 && rnd.NextDouble() < 0.45;
                var qtd = entrada ? rnd.Next(2, Math.Min(saldo, 20) + 1) : rnd.Next(1, 6);
                var delta = entrada ? qtd : -qtd;

                lista.Add(new Movimentacao
                {
                    ProdutoId = p.Id,
                    ProdutoNome = p.Nome,
                    Tipo = entrada ? TiposMovimentacao.Entrada : TiposMovimentacao.Saida,
                    Quantidade = delta,
                    SaldoApos = saldo,
                    UsuarioNome = nomes[rnd.Next(nomes.Count)],
                    Data = quando
                });

                saldo -= delta;
                quando = quando.AddHours(-rnd.Next(10, 80));
            }
        }

        db.Movimentacoes.AddRange(lista.OrderBy(m => m.Data));
        await db.SaveChangesAsync(ct);
    }
}
