namespace VMTO.Worker.Messages;

public sealed record ApplyDeltaMessage(
    Guid JobId,
    Guid StepId,
    string DeltaStorageKey,
    string TargetStorageKey,
    Guid CorrelationId);
