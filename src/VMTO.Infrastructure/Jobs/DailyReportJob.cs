using Microsoft.EntityFrameworkCore;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Enums;
using VMTO.Infrastructure.Persistence;

namespace VMTO.Infrastructure.Jobs;

public sealed class DailyReportJob(
    AppDbContext db,
    IWebhookService webhookService)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-1);
        var baseQuery = db.Jobs.Where(j => j.CreatedAt >= since);

        var payload = new
        {
            action = "daily-report",
            generatedAt = DateTime.UtcNow,
            timeRangeHours = 24,
            totalJobs = await baseQuery.CountAsync(ct),
            succeededJobs = await baseQuery.CountAsync(j => j.Status == JobStatus.Succeeded, ct),
            failedJobs = await baseQuery.CountAsync(j => j.Status == JobStatus.Failed, ct),
            runningJobs = await baseQuery.CountAsync(j => j.Status == JobStatus.Running, ct),
            queuedJobs = await baseQuery.CountAsync(j => j.Status == JobStatus.Queued, ct)
        };

        await webhookService.NotifySystemAnnouncementAsync("SystemAnnouncement", payload, ct);
    }
}
