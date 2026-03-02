using VMTO.Domain.Enums;
using VMTO.Domain.Events;
using VMTO.Shared;

namespace VMTO.Domain.Aggregates.MigrationJob;

public sealed class JobStep
{
    private readonly List<IDomainEvent> _domainEvents = [];

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

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

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

    public void ClearDomainEvents() => _domainEvents.Clear();

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
        _domainEvents.Add(new StepCompletedEvent(JobId, Id, Name));
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
        _domainEvents.Add(new StepFailedEvent(JobId, Id, Name, error));
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
        Progress = 0;
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

    public Result SetLogsUri(string uri)
    {
        if (Status is not (StepStatus.Running or StepStatus.Succeeded))
            return Result.Failure(ErrorCodes.Job.InvalidTransition,
                $"Cannot set logs URI for step '{Name}' in status {Status}.");

        LogsUri = uri;
        return Result.Success();
    }
}
