namespace VMTO.Domain.Events;

public sealed record StepCompletedEvent(
    Guid JobId,
    Guid StepId,
    string StepName) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
