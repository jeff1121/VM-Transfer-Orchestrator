using VMTO.Shared;

namespace VMTO.Domain.Events;

public sealed record JobCreatedEvent(
    Guid JobId,
    CorrelationId CorrelationId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
