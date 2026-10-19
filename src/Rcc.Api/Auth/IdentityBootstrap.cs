using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rcc.Api.Data;

namespace Rcc.Api.Auth;

public static class IdentityBootstrap
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<RccDbContext>();
        await database.Database.MigrateAsync();
        var email = configuration["Administrator:Email"]?.Trim();
        var password = configuration["Administrator:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            throw new InvalidOperationException("Configure Administrator:Email y Administrator:Password mediante secretos locales.");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!await users.IsInRoleAsync(existing, "Admin"))
                throw new InvalidOperationException("La cuenta ya existe como cliente. No se promueve automáticamente.");
            Console.WriteLine("Administrador existente; no se modifica su contraseña.");
            return;
        }
        await using var transaction = await database.Database.BeginTransactionAsync();
        var user = new IdentityUser { UserName = email, Email = email };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded) throw new InvalidOperationException("Administrador inválido: " + string.Join(", ", created.Errors.Select(x => x.Code)));
        var role = await users.AddToRoleAsync(user, "Admin");
        if (!role.Succeeded) throw new InvalidOperationException("No se pudo asignar el rol administrador.");
        await transaction.CommitAsync();
        Console.WriteLine("Administrador creado. Retire Administrator:Password de la configuración después de inicializar.");
    }
}
