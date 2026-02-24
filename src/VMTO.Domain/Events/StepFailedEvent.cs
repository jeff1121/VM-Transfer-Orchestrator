namespace VMTO.Domain.Events;

public sealed record StepFailedEvent(
    Guid JobId,
    Guid StepId,
    string StepName,
    string Error) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
