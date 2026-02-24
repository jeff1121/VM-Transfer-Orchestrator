namespace VMTO.Domain.Events;

public sealed record ArtifactUploadedEvent(
    Guid JobId,
    Guid ArtifactId,
    string StorageUri) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
