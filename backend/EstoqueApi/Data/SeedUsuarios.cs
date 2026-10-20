using EstoqueApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EstoqueApi.Data;

/// <summary>
/// Cria os usuários definidos em "Seed:Usuarios" (appsettings.Development.json, user-secrets
/// ou variáveis de ambiente). Nenhuma senha fica fixa no código.
/// </summary>
public static class SeedUsuarios
{
    public record Item(string Email, string Nome, string Senha, string Perfil);

    public static void Executar(AppDbContext db, IConfiguration cfg, IPasswordHasher<Usuario> hasher, ILogger log)
    {
        var itens = cfg.GetSection("Seed:Usuarios").Get<Item[]>() ?? [];
        foreach (var i in itens)
        {
            var email = i.Email.Trim().ToLowerInvariant();
            if (db.Usuarios.Any(u => u.Email == email)) continue;
            if (!Perfis.Todos.Contains(i.Perfil) || i.Senha.Length < 8)
            {
                log.LogWarning("Usuário de seed ignorado ({Email}): perfil inválido ou senha com menos de 8 caracteres.", email);
                continue;
            }
            var u = new Usuario { Email = email, Nome = i.Nome, Perfil = i.Perfil };
            u.SenhaHash = hasher.HashPassword(u, i.Senha);
            db.Usuarios.Add(u);
            log.LogInformation("Usuário inicial criado: {Email} ({Perfil})", email, i.Perfil);
        }
        db.SaveChanges();

        if (!db.Usuarios.AsNoTracking().Any())
            log.LogWarning("Nenhum usuário cadastrado. Configure 'Seed:Usuarios' para conseguir fazer login.");
    }
}
