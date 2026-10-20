using System.ComponentModel.DataAnnotations;

namespace EstoqueApi.Models;

public static class Perfis
{
    public const string Admin = "Admin";
    public const string Operador = "Operador";
    public static readonly string[] Todos = [Admin, Operador];
}

public class Usuario
{
    public int Id { get; set; }

    [MaxLength(160)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Hash PBKDF2 (ASP.NET Core Identity). A senha nunca é armazenada.</summary>
    public string SenhaHash { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Perfil { get; set; } = Perfis.Operador;

    public bool Ativo { get; set; } = true;

    /// <summary>Tentativas de login malsucedidas consecutivas (para bloqueio temporário).</summary>
    public int FalhasLogin { get; set; }

    public DateTime? BloqueadoAte { get; set; }

    /// <summary>Muda a cada troca de senha/desativação, invalidando sessões antigas.</summary>
    public string CarimboSeguranca { get; set; } = Guid.NewGuid().ToString("N");
}

public record LoginInput
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(160)]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [StringLength(128)]
    public string Senha { get; init; } = string.Empty;
}

public record UsuarioDto(int Id, string Nome, string Email, string Perfil)
{
    public static UsuarioDto De(Usuario u) => new(u.Id, u.Nome, u.Email, u.Perfil);
}
