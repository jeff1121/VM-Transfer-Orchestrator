using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace VMTO.Infrastructure.Telemetry.HealthChecks;

public sealed class DiskSpaceHealthCheck(IConfiguration configuration) : IHealthCheck
{
    private const long DefaultMinimumFreeBytes = 10L * 1024 * 1024 * 1024; // 10GB

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = configuration["HealthChecks:Disk:Path"] ?? AppContext.BaseDirectory;
            var minimumFreeBytes = configuration.GetValue<long?>("HealthChecks:Disk:MinimumFreeBytes") ?? DefaultMinimumFreeBytes;

            var rootPath = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Unable to resolve disk root path."));
            }

            var drive = DriveInfo.GetDrives().FirstOrDefault(d =>
                string.Equals(d.Name, rootPath, StringComparison.OrdinalIgnoreCase));

            if (drive is null || !drive.IsReady)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy($"Disk '{rootPath}' is unavailable."));
            }

            if (drive.AvailableFreeSpace < minimumFreeBytes)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Disk free space is below threshold. free={drive.AvailableFreeSpace} threshold={minimumFreeBytes}"));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Disk free space is healthy. free={drive.AvailableFreeSpace} threshold={minimumFreeBytes}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Unable to verify disk free space.", ex));
        }
    }
}
