using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;
using VMTO.Infrastructure.Persistence;
using VMTO.Infrastructure.Resilience;

namespace VMTO.Infrastructure.Ops;

public sealed partial class SelfHealingService(
    AppDbContext db,
    IErrorClassifier errorClassifier,
    IOptions<OpsAutomationOptions> options,
    ILogger<SelfHealingService> logger)
{
    private readonly OpsAutomationOptions _options = options.Value;

    public async Task<int> ScanAndHealAsync(CancellationToken ct = default)
    {
        var thresholdMinutes = Math.Max(5, _options.StuckJobThresholdMinutes);
        var cutoff = DateTime.UtcNow.AddMinutes(-thresholdMinutes);

        var stuckJobs = await db.Jobs
            .Include(j => j.Steps)
            .Where(j => j.Status == JobStatus.Running && j.UpdatedAt < cutoff)
            .ToListAsync(ct);

        if (stuckJobs.Count == 0)
        {
            return 0;
        }

        var healed = 0;
        foreach (var job in stuckJobs)
        {
            job.Fail($"Job marked as stuck after {thresholdMinutes} minutes without update.");

            var requeued = CloneAsQueued(job);
            await db.Jobs.AddAsync(requeued, ct);

            LogStuckJobHealed(logger, job.Id, requeued.Id);
            healed++;
        }

        await db.SaveChangesAsync(ct);
        return healed;
    }

    public async Task<int> HandleFailedJobsAsync(CancellationToken ct = default)
    {
        var delayMinutes = Math.Max(1, _options.FailedJobRetryDelayMinutes);
        var cutoff = DateTime.UtcNow.AddMinutes(-delayMinutes);

        var failedJobs = await db.Jobs
            .Include(j => j.Steps)
            .Where(j => j.Status == JobStatus.Failed && j.UpdatedAt < cutoff)
            .ToListAsync(ct);

        if (failedJobs.Count == 0)
        {
            return 0;
        }

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
            }
        }

        if (autoRetried > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return autoRetried;
    }

    public async Task<IReadOnlyList<MigrationJob>> ListStuckJobsAsync(CancellationToken ct = default)
    {
        var thresholdMinutes = Math.Max(5, _options.StuckJobThresholdMinutes);
        var cutoff = DateTime.UtcNow.AddMinutes(-thresholdMinutes);

        return await db.Jobs
            .Include(j => j.Steps)
            .Where(j => j.Status == JobStatus.Running && j.UpdatedAt < cutoff)
            .ToListAsync(ct);
    }

    public async Task<bool> HealStuckJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await db.Jobs
            .Include(j => j.Steps)
            .FirstOrDefaultAsync(j => j.Id == jobId && j.Status == JobStatus.Running, ct);

        if (job is null)
        {
            return false;
        }

        job.Fail("Job manually healed by ops trigger.");
        var requeued = CloneAsQueued(job);
        await db.Jobs.AddAsync(requeued, ct);
        LogStuckJobHealed(logger, job.Id, requeued.Id);
        await db.SaveChangesAsync(ct);
        return true;
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
