using VMTO.Domain.Enums;
using VMTO.Domain.Events;
using VMTO.Domain.ValueObjects;
using VMTO.Shared;

namespace VMTO.Domain.Aggregates.MigrationJob;

public sealed class MigrationJob
{
    private readonly List<JobStep> _steps = [];
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; private set; }
    public CorrelationId CorrelationId { get; private set; }
    public Guid SourceConnectionId { get; private set; }
    public Guid TargetConnectionId { get; private set; }
    public StorageTarget StorageTarget { get; private set; }
    public MigrationStrategy Strategy { get; private set; }
    public MigrationOptions Options { get; private set; }
    public JobStatus Status { get; private set; }
    public int Progress { get; private set; }
    public string? Result { get; private set; }
    public IReadOnlyList<JobStep> Steps => _steps.AsReadOnly();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public MigrationJob(
        Guid sourceConnectionId,
        Guid targetConnectionId,
        StorageTarget storageTarget,
        MigrationStrategy strategy,
        MigrationOptions options,
        CorrelationId? correlationId = null)
    {
        Id = Guid.NewGuid();
        CorrelationId = correlationId ?? CorrelationId.New();
        SourceConnectionId = sourceConnectionId;
        TargetConnectionId = targetConnectionId;
        StorageTarget = storageTarget;
        Strategy = strategy;
        Options = options;
        Status = JobStatus.Created;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;

        RaiseDomainEvent(new JobCreatedEvent(Id, CorrelationId));
    }

    // EF Core / serialization
    private MigrationJob()
    {
        StorageTarget = null!;
        Options = null!;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    public Result Enqueue()
    {
        return Transition(JobStatus.Created, JobStatus.Queued);
    }

    public Result Start()
    {
        return Transition(JobStatus.Queued, JobStatus.Running);
    }

    public Result RequestPause()
    {
        return Transition(JobStatus.Running, JobStatus.Pausing);
    }

    public Result Pause()
    {
        return Transition(JobStatus.Pausing, JobStatus.Paused);
    }

    public Result RequestResume()
    {
        return Transition(JobStatus.Paused, JobStatus.Resuming);
    }

    public Result Resume()
    {
        return Transition(JobStatus.Resuming, JobStatus.Running);
    }

    public Result RequestCancel()
    {
        if (Status is not (JobStatus.Running or JobStatus.Pausing or JobStatus.Paused or JobStatus.Queued))
            return InvalidTransition(JobStatus.Cancelling);

        return Transition(Status, JobStatus.Cancelling);
    }

    public Result Cancel()
    {
        return Transition(JobStatus.Cancelling, JobStatus.Cancelled);
    }

    public Result Fail(string reason)
    {
        if (Status is not (JobStatus.Running or JobStatus.Resuming))
            return InvalidTransition(JobStatus.Failed);

        Result = reason;
        return Transition(Status, JobStatus.Failed);
    }

    public Result Complete()
    {
        if (Status is not JobStatus.Running)
            return InvalidTransition(JobStatus.Succeeded);

        if (!_steps.TrueForAll(s => s.Status is StepStatus.Succeeded or StepStatus.Skipped))
            return Shared.Result.Failure(ErrorCodes.Job.InvalidTransition,
                "Cannot complete job: not all steps have succeeded.");

        return Transition(Status, JobStatus.Succeeded);
    }

    public void AddStep(string name, int order)
    {
        var step = new JobStep(Id, name, order, Options.MaxRetries);
        _steps.Add(step);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProgress()
    {
        if (_steps.Count == 0)
        {
            Progress = 0;
            return;
        }

        Progress = (int)_steps.Average(s => s.Progress);
        UpdatedAt = DateTime.UtcNow;
    }

    private Result Transition(JobStatus requiredCurrent, JobStatus next)
    {
        if (Status != requiredCurrent)
            return InvalidTransition(next);

        var old = Status;
        Status = next;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new JobStatusChangedEvent(Id, old, next));
        return Shared.Result.Success();
    }

    private Result InvalidTransition(JobStatus target)
    {
        return Shared.Result.Failure(ErrorCodes.Job.InvalidTransition,
            $"Cannot transition from {Status} to {target}.");
    }

    private void RaiseDomainEvent(IDomainEvent @event) => _domainEvents.Add(@event);
}
