using EstoqueApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EstoqueApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Produto> Produtos => Set<Produto>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // SQLite não suporta decimal nativamente; armazenamos como double
        b.Entity<Produto>().Property(p => p.Preco).HasConversion<double>();

        b.Entity<Produto>().HasData(
            new Produto { Id = 1, Nome = "Teclado Mecânico", Categoria = "Periféricos", Preco = 349.90m, Quantidade = 25, EstoqueMinimo = 5, AtualizadoEm = new DateTime(2026, 1, 1) },
            new Produto { Id = 2, Nome = "Mouse Gamer", Categoria = "Periféricos", Preco = 189.90m, Quantidade = 3, EstoqueMinimo = 8, AtualizadoEm = new DateTime(2026, 1, 1) },
            new Produto { Id = 3, Nome = "Monitor 27\" 4K", Categoria = "Monitores", Preco = 2199.00m, Quantidade = 12, EstoqueMinimo = 4, AtualizadoEm = new DateTime(2026, 1, 1) },
            new Produto { Id = 4, Nome = "SSD NVMe 1TB", Categoria = "Armazenamento", Preco = 499.00m, Quantidade = 0, EstoqueMinimo = 6, AtualizadoEm = new DateTime(2026, 1, 1) }
        );
    }
}
