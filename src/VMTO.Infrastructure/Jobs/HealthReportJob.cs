using Microsoft.EntityFrameworkCore;
using VMTO.Domain.Enums;
using VMTO.Infrastructure.Ops;
using VMTO.Infrastructure.Persistence;

namespace VMTO.Infrastructure.Jobs;

public sealed class HealthReportJob(
    AppDbContext db,
    IOpsSnapshotStore snapshotStore)
{
    public async Task<HealthSnapshot> RunAsync(CancellationToken ct = default)
    {
        var databaseReachable = await db.Database.CanConnectAsync(ct);
        var snapshot = new HealthSnapshot(
            GeneratedAt: DateTime.UtcNow,
            DatabaseReachable: databaseReachable,
            TotalJobs: await db.Jobs.CountAsync(ct),
            RunningJobs: await db.Jobs.CountAsync(j => j.Status == JobStatus.Running, ct),
            FailedJobs: await db.Jobs.CountAsync(j => j.Status == JobStatus.Failed, ct),
            QueuedJobs: await db.Jobs.CountAsync(j => j.Status == JobStatus.Queued, ct));

        snapshotStore.AddHealthSnapshot(snapshot);
        return snapshot;
    }
}
