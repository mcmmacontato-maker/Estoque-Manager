using EstoqueApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EstoqueApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Movimentacao> Movimentacoes => Set<Movimentacao>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Usuario>().HasIndex(u => u.Email).IsUnique();
        b.Entity<Movimentacao>().HasIndex(m => m.Data);
        b.Entity<Movimentacao>().HasIndex(m => m.ProdutoId);
        // SQLite não suporta decimal nativamente; armazenamos como double
        b.Entity<Produto>().Property(p => p.Preco).HasConversion<double>();

        b.Entity<Produto>().HasData(ProdutosIniciais());

        // SQLite não guarda o "Kind" da data: marca tudo como UTC para a API serializar com "Z"
        var utc = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var utcNull = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? v.Value.ToUniversalTime() : v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
        foreach (var prop in b.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
        {
            if (prop.ClrType == typeof(DateTime)) prop.SetValueConverter(utc);
            else if (prop.ClrType == typeof(DateTime?)) prop.SetValueConverter(utcNull);
        }
    }

    /// <summary>Catálogo inicial (usado na criação do banco e no reset do modo demonstração).</summary>
    public static Produto[] ProdutosIniciais()
    {
        var d = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Produto P(int id, string nome, string cat, decimal preco, int qtd, int min) =>
            new() { Id = id, Nome = nome, Categoria = cat, Preco = preco, Quantidade = qtd, EstoqueMinimo = min, AtualizadoEm = d };

        return
        [
            P(1,  "Teclado Mecânico ABNT2",      "Periféricos",   349.90m, 25, 5),
            P(2,  "Mouse Sem Fio Ergonômico",    "Periféricos",   189.90m,  3, 8),
            P(3,  "Monitor 27\" 4K IPS",         "Monitores",    2199.00m, 12, 4),
            P(4,  "SSD NVMe 1TB",                "Armazenamento", 499.00m,  0, 6),
            P(5,  "Headset USB com Microfone",   "Áudio",         279.00m, 18, 5),
            P(6,  "Webcam Full HD 1080p",        "Periféricos",   329.90m,  4, 5),
            P(7,  "Notebook 14\" i5 16GB",       "Computadores", 4899.00m,  7, 3),
            P(8,  "Hub USB-C 7 em 1",            "Acessórios",    219.90m, 40, 10),
            P(9,  "Cadeira Ergonômica",          "Mobiliário",   1599.00m,  2, 3),
            P(10, "Suporte Articulado p/ Monitor","Acessórios",   259.00m, 15, 4),
            P(11, "HD Externo 2TB",              "Armazenamento", 459.90m,  9, 4),
            P(12, "Roteador Wi-Fi 6",            "Redes",         699.00m,  0, 2),
        ];
    }
}
