using System.ComponentModel.DataAnnotations;

namespace EstoqueApi.Models;

/// <summary>
/// Dados aceitos do cliente para criar/editar um produto.
/// Separado da entidade para impedir que o cliente defina Id ou AtualizadoEm (mass assignment).
/// </summary>
public record ProdutoInput
{
    [Required(ErrorMessage = "Nome é obrigatório.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Nome deve ter entre 2 e 120 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    [StringLength(60, ErrorMessage = "Categoria deve ter no máximo 60 caracteres.")]
    public string? Categoria { get; init; }

    [Range(typeof(decimal), "0", "9999999", ErrorMessage = "Preço deve estar entre 0 e 9.999.999.")]
    public decimal Preco { get; init; }

    [Range(0, 1_000_000, ErrorMessage = "Quantidade deve estar entre 0 e 1.000.000.")]
    public int Quantidade { get; init; }

    [Range(0, 1_000_000, ErrorMessage = "Estoque mínimo deve estar entre 0 e 1.000.000.")]
    public int EstoqueMinimo { get; init; } = 5;

    public void AplicarEm(Produto p)
    {
        p.Nome = Nome.Trim();
        p.Categoria = string.IsNullOrWhiteSpace(Categoria) ? "Geral" : Categoria.Trim();
        p.Preco = decimal.Round(Preco, 2);
        p.Quantidade = Quantidade;
        p.EstoqueMinimo = EstoqueMinimo;
        p.AtualizadoEm = DateTime.UtcNow;
    }
}
