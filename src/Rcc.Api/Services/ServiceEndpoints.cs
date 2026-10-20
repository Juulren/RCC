using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rcc.Api.Data;
namespace Rcc.Api.Services;

public static class ServiceEndpoints
{
    public static void MapRccServices(this WebApplication app)
    {
        app.MapGet("/api/weather", async (WeatherService weather, CancellationToken token) => Results.Ok(await weather.GetAsync(token)));
        app.MapGet("/api/location", (IConfiguration config) =>
        {
            var key = config["GoogleMaps:EmbedKey"];
            var query = Uri.EscapeDataString("Regent Chongqing 66 Jinshamen Road Chongqing China");
            return Results.Ok(new
            {
                searchUrl = "https://www.google.com/maps/search/?api=1&query=" + query,
                embedUrl = string.IsNullOrWhiteSpace(key) ? null : "https://www.google.com/maps/embed/v1/place?key=" + Uri.EscapeDataString(key) + "&q=" + query
            });
        });
        app.MapGet("/api/admin/notifications", async (HttpContext context, RccDbContext db, IOptions<EmailSettings> settings, int page = 1) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (page is < 1 or > 1000000) return Results.BadRequest();
            var query = db.Notifications.AsNoTracking();
            var total = await query.CountAsync();
            var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * 20).Take(20)
                .Select(x => new { x.Id, x.ReservationId, x.Kind, x.State, x.Attempts, x.CreatedAt, x.NextAttemptAt, x.LastError }).ToListAsync();
            return Results.Ok(new { configured = settings.Value.Ready, items, total, page, totalPages = (int)Math.Ceiling(total / 20d) });
        }).RequireAuthorization(policy => policy.RequireRole("Admin"));
    }
}
