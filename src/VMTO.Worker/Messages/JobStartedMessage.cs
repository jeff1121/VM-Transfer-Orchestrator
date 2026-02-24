namespace VMTO.Worker.Messages;

public sealed record JobStartedMessage(
    Guid JobId,
    List<string> StepNames,
    Guid SourceConnectionId,
    Guid TargetConnectionId,
    string StorageEndpoint,
    string StorageBucket,
    Guid CorrelationId);
