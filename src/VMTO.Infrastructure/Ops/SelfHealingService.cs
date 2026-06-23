using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;
using VMTO.Infrastructure.Persistence;
using VMTO.Infrastructure.Resilience;

namespace VMTO.Infrastructure.Ops;

public sealed record StuckJobInfo(Guid JobId, DateTime UpdatedAt, int Progress, string Status);

public sealed partial class SelfHealingService(
    AppDbContext db,
    IErrorClassifier errorClassifier,
    IWebhookService webhookService,
    IOptions<OpsAutomationOptions> options,
    ILogger<SelfHealingService> logger)
{
    private readonly OpsAutomationOptions _options = options.Value;

    public async Task<IReadOnlyList<StuckJobInfo>> ListStuckJobsAsync(CancellationToken ct = default)
    {
        var threshold = DateTime.UtcNow.AddMinutes(-Math.Max(1, _options.StuckJobThresholdMinutes));
        return await db.Jobs
            .Where(j => j.Status == JobStatus.Running && j.UpdatedAt < threshold)
            .OrderBy(j => j.UpdatedAt)
            .Select(j => new StuckJobInfo(j.Id, j.UpdatedAt, j.Progress, j.Status.ToString()))
            .ToListAsync(ct);
    }

    public async Task<bool> HealStuckJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await db.Jobs.Include(j => j.Steps).FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null || job.Status != JobStatus.Running)
        {
            return false;
        }

        var cancelRequested = job.RequestCancel();
        if (!cancelRequested.IsSuccess)
        {
            return false;
        }

        var cancelled = job.Cancel();
        if (!cancelled.IsSuccess)
        {
            return false;
        }

        var requeued = CloneAsQueued(job);
        await db.Jobs.AddAsync(requeued, ct);
        await db.SaveChangesAsync(ct);

        await webhookService.NotifySystemAnnouncementAsync("SystemAnnouncement", new
        {
            action = "stuck-job-healed",
            sourceJobId = job.Id,
            requeuedJobId = requeued.Id,
            at = DateTime.UtcNow
        }, ct);

        LogStuckJobHealed(logger, job.Id, requeued.Id);
        return true;
    }

    public async Task<int> ScanAndHealAsync(CancellationToken ct = default)
    {
        var stuckJobs = await ListStuckJobsAsync(ct);
        var healed = 0;
        foreach (var stuck in stuckJobs)
        {
            if (await HealStuckJobAsync(stuck.JobId, ct))
            {
                healed++;
            }
        }

        return healed;
    }

    public async Task<int> HandleFailedJobsAsync(CancellationToken ct = default)
    {
        var delay = Math.Max(1, _options.FailedJobRetryDelayMinutes);
        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-(delay + 5));
        var windowEnd = now.AddMinutes(-delay);

        var failedJobs = await db.Jobs
            .Include(j => j.Steps)
            .Where(j => j.Status == JobStatus.Failed && j.UpdatedAt >= windowStart && j.UpdatedAt <= windowEnd)
            .ToListAsync(ct);

        var autoRetried = 0;
        foreach (var job in failedJobs)
        {
            var failedStep = job.Steps
                .Where(s => s.Status == StepStatus.Failed)
                .OrderByDescending(s => s.CompletedAt)
                .FirstOrDefault();

            var category = errorClassifier.Classify(failedStep?.ErrorMessage);
            if (category == ErrorCategory.Transient)
            {
                var retryJob = CloneAsQueued(job);
                await db.Jobs.AddAsync(retryJob, ct);
                autoRetried++;

                await webhookService.NotifySystemAnnouncementAsync("SystemAnnouncement", new
                {
                    action = "auto-retry-enqueued",
                    sourceJobId = job.Id,
                    retryJobId = retryJob.Id,
                    reason = failedStep?.ErrorMessage,
                    at = DateTime.UtcNow
                }, ct);
            }
            else
            {
                await webhookService.NotifyJobCompletedAsync(
                    job.Id,
                    "Failed",
                    failedStep?.ErrorMessage ?? "Permanent failure",
                    ct);
            }
        }

        if (autoRetried > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return autoRetried;
    }

    private static MigrationJob CloneAsQueued(MigrationJob source)
    {
        var clone = new MigrationJob(
            source.SourceConnectionId,
            source.TargetConnectionId,
            source.StorageTarget,
            source.Strategy,
            source.Options);

        foreach (var step in source.Steps.OrderBy(s => s.Order))
        {
            clone.AddStep(step.Name, step.Order);
        }

        clone.Enqueue();
        return clone;
    }

    [LoggerMessage(EventId = 9701, Level = LogLevel.Warning,
        Message = "Stuck job healed. SourceJobId={SourceJobId}, RequeuedJobId={RequeuedJobId}")]
    private static partial void LogStuckJobHealed(ILogger logger, Guid sourceJobId, Guid requeuedJobId);
}
