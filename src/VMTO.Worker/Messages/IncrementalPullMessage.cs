namespace VMTO.Worker.Messages;

public sealed record IncrementalPullMessage(
    Guid JobId,
    Guid StepId,
    Guid SourceConnectionId,
    string VmId,
    string ChangeId,
    string BaseStorageKey,
    Guid CorrelationId);
