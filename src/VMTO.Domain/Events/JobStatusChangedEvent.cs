using VMTO.Domain.Enums;

namespace VMTO.Domain.Events;

public sealed record JobStatusChangedEvent(
    Guid JobId,
    JobStatus OldStatus,
    JobStatus NewStatus) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
