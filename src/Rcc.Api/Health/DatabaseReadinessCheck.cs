using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Rcc.Api.Data;

namespace Rcc.Api.Health;

public sealed class DatabaseReadinessCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<RccDbContext>().Database;
            if (!await database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy();
            }

            var applied = await database.GetAppliedMigrationsAsync(cancellationToken);
            var pending = await database.GetPendingMigrationsAsync(cancellationToken);
            return applied.Any() && !pending.Any()
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Public response omits database errors and connection strings.
            return HealthCheckResult.Unhealthy();
        }
    }
}
