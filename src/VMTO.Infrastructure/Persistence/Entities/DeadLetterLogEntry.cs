namespace VMTO.Infrastructure.Persistence.Entities;

public sealed class DeadLetterLogEntry
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReplayedAt { get; set; }
}
