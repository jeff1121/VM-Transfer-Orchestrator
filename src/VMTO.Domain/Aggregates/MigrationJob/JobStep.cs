using VMTO.Domain.Enums;
using VMTO.Shared;

namespace VMTO.Domain.Aggregates.MigrationJob;

public sealed class JobStep
{
    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public string Name { get; private set; }
    public int Order { get; private set; }
    public StepStatus Status { get; private set; }
    public int Progress { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? LogsUri { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public JobStep(Guid jobId, string name, int order, int maxRetries)
    {
        Id = Guid.NewGuid();
        JobId = jobId;
        Name = name;
        Order = order;
        Status = StepStatus.Pending;
        MaxRetries = maxRetries;
    }

    // EF Core / serialization
    private JobStep() { Name = string.Empty; }

    public Result Start()
    {
        if (Status is not StepStatus.Pending and not StepStatus.Retrying)
            return Result.Failure(ErrorCodes.Job.InvalidTransition,
                $"Cannot start step '{Name}' from status {Status}.");

        Status = StepStatus.Running;
        StartedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Complete()
    {
        if (Status is not StepStatus.Running)
            return Result.Failure(ErrorCodes.Job.InvalidTransition,
                $"Cannot complete step '{Name}' from status {Status}.");

        Status = StepStatus.Succeeded;
        Progress = 100;
        CompletedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Fail(string error)
    {
        if (Status is not StepStatus.Running and not StepStatus.Retrying)
            return Result.Failure(ErrorCodes.Job.InvalidTransition,
                $"Cannot fail step '{Name}' from status {Status}.");

        ErrorMessage = error;
        Status = StepStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Skip()
    {
        if (Status is not StepStatus.Pending)
            return Result.Failure(ErrorCodes.Job.InvalidTransition,
                $"Cannot skip step '{Name}' from status {Status}.");

        Status = StepStatus.Skipped;
        CompletedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Retry()
    {
        if (Status is not StepStatus.Failed)
            return Result.Failure(ErrorCodes.Job.InvalidTransition,
                $"Cannot retry step '{Name}' from status {Status}.");

        if (RetryCount >= MaxRetries)
            return Result.Failure(ErrorCodes.Job.InvalidTransition,
                $"Step '{Name}' has exceeded max retries ({MaxRetries}).");

        RetryCount++;
        Status = StepStatus.Retrying;
        ErrorMessage = null;
        CompletedAt = null;
        return Result.Success();
    }

    public Result UpdateProgress(int percent)
    {
        if (Status is not StepStatus.Running)
            return Result.Failure(ErrorCodes.Job.InvalidTransition,
                $"Cannot update progress for step '{Name}' in status {Status}.");

        Progress = Math.Clamp(percent, 0, 100);
        return Result.Success();
    }
}
