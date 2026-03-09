using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VMTO.Infrastructure.Persistence;

namespace VMTO.Infrastructure.Telemetry.HealthChecks;

public sealed class DbMigrationHealthCheck(AppDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            var pendingCount = pendingMigrations.Count();
            return pendingCount == 0
                ? HealthCheckResult.Healthy("Database migrations are up to date.")
                : HealthCheckResult.Unhealthy($"Database has {pendingCount} pending migrations.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Unable to verify database migrations.", ex);
        }
    }
}
