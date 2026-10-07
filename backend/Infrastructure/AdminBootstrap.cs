using backend.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure;

public static class AdminBootstrap
{
    public static async Task InitializeAsync(AppDbContext context, IConfiguration configuration)
    {
        var email = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_EMAIL") ?? configuration["Bootstrap:AdminEmail"];
        var password = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD") ?? configuration["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password)) return;
        if (await context.Users.AnyAsync(u => u.Role == "Administrador")) return;
        if (!backend.Application.Services.ValidationHelper.IsValidEmail(email ?? "") || string.IsNullOrEmpty(password) || password.Length < 12)
            throw new InvalidOperationException("Configure o e-mail do administrador inicial e uma senha de pelo menos 12 caracteres.");
        var normalized = email!.Trim().ToLowerInvariant();
        if (await context.Users.AnyAsync(u => u.Email.ToLower() == normalized))
            throw new InvalidOperationException("O identificador do administrador inicial já pertence a outro usuário.");
        var user = new User { Id = Guid.NewGuid(), Email = normalized, Role = "Administrador", Status = "Aprovado" };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        user.Profile = new UserProfile { Id = user.Id, Nome = "Administrador", CPF = "", Telefone = "", DataNascimento = DateTime.UtcNow.AddYears(-18) };
        context.Users.Add(user);
        await context.SaveChangesAsync();
    }
}
