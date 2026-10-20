using System.ComponentModel.DataAnnotations;

namespace EstoqueApi.Models;

public class Produto
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(60)]
    public string Categoria { get; set; } = "Geral";

    [Range(0, double.MaxValue)]
    public decimal Preco { get; set; }

    [Range(0, int.MaxValue)]
    public int Quantidade { get; set; }

    [Range(0, int.MaxValue)]
    public int EstoqueMinimo { get; set; } = 5;

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}
