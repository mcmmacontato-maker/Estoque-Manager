namespace EstoqueApi.Models;

public static class TiposMovimentacao
{
    public const string Entrada = "Entrada";
    public const string Saida = "Saída";
    public const string Ajuste = "Ajuste";
    public const string Cadastro = "Cadastro";
    public static readonly string[] Todos = [Entrada, Saida, Ajuste, Cadastro];
}

/// <summary>
/// Registro imutável de cada alteração de quantidade (trilha de auditoria).
/// Guarda o nome do produto e do usuário para o histórico continuar legível mesmo após exclusões.
/// </summary>
public class Movimentacao
{
    public long Id { get; set; }
    public int ProdutoId { get; set; }
    public string ProdutoNome { get; set; } = string.Empty;
    public string Tipo { get; set; } = TiposMovimentacao.Entrada;

    /// <summary>Variação com sinal (+ entrada, − saída).</summary>
    public int Quantidade { get; set; }

    public int SaldoApos { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public DateTime Data { get; set; } = DateTime.UtcNow;
}
