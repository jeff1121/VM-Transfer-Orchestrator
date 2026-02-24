namespace VMTO.Worker.Messages;

public sealed record FinalSyncCutoverMessage(
    Guid JobId,
    Guid StepId,
    Guid SourceConnectionId,
    Guid TargetConnectionId,
    string VmId,
    int PveVmId,
    Guid CorrelationId);
