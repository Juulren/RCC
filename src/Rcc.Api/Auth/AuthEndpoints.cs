using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Rcc.Api.Data;

namespace Rcc.Api.Auth;

public static class AuthEndpoints
{
    public static void MapRccAuth(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            if (HttpMethods.IsPost(context.HttpContext.Request.Method))
            {
                try { await context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context.HttpContext); }
                catch (AntiforgeryValidationException) { return Error(400, "CSRF_INVALID"); }
            }
            return await next(context);
        });
        group.MapGet("/csrf", (HttpContext context, IAntiforgery csrf) =>
            Results.Ok(new { token = csrf.GetAndStoreTokens(context).RequestToken }));
        group.MapPost("/register", Register).RequireRateLimiting("auth");
        group.MapPost("/login", Login).RequireRateLimiting("auth");
        group.MapPost("/logout", async (HttpContext context, UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn) =>
        {
            var user = await users.GetUserAsync(context.User);
            if (user is null) return Results.Unauthorized();
            var updated = await users.UpdateSecurityStampAsync(user);
            if (!updated.Succeeded) return Error(503, "SESSION_UPDATE_FAILED");
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();
        group.MapGet("/me", async (HttpContext context, UserManager<IdentityUser> users) =>
        {
            var user = await users.GetUserAsync(context.User);
            return user is null ? Results.Unauthorized() : Results.Ok(new { user.Id, user.Email, roles = await users.GetRolesAsync(user) });
        }).RequireAuthorization();
        app.MapGet("/api/admin/access", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { role = "Admin", module = 3 });
        }).RequireAuthorization(policy => policy.RequireRole("Admin"));
    }

    private static async Task<IResult> Register(Credentials input, UserManager<IdentityUser> users, RccDbContext database)
    {
        var email = input.Email?.Trim();
        if (!ValidEmail(email) || input.Password is null || input.Password.Length is < 12 or > 128)
            return Error(400, "VALIDATION_FAILED");
        if (await users.FindByEmailAsync(email!) is not null) return Error(409, "EMAIL_UNAVAILABLE");
        await using var transaction = await database.Database.BeginTransactionAsync();
        try
        {
            var user = new IdentityUser { Email = email, UserName = email };
            var created = await users.CreateAsync(user, input.Password);
            if (!created.Succeeded) return created.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName")
                ? Error(409, "EMAIL_UNAVAILABLE") : Error(400, "VALIDATION_FAILED");
            var role = await users.AddToRoleAsync(user, "Customer");
            if (!role.Succeeded) return Error(503, "REGISTRATION_FAILED");
            await transaction.CommitAsync();
            return Results.Created("/api/auth/me", new { user.Id, user.Email, roles = new[] { "Customer" } });
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return Error(409, "EMAIL_UNAVAILABLE"); }
    }

    private static async Task<IResult> Login(Credentials input, SignInManager<IdentityUser> signIn)
    {
        var email = input.Email?.Trim();
        if (!ValidEmail(email) || input.Password is null || input.Password.Length is < 1 or > 128)
            return Error(401, "INVALID_CREDENTIALS");
        var result = await signIn.PasswordSignInAsync(email!, input.Password, isPersistent: false, lockoutOnFailure: true);
        return result.Succeeded ? Results.NoContent() : Error(401, "INVALID_CREDENTIALS");
    }

    private static bool ValidEmail(string? email) => email is { Length: > 0 and <= 254 } && new EmailAddressAttribute().IsValid(email);
    private static IResult Error(int status, string code) => Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code });
    public sealed record Credentials(string? Email, string? Password);
}
