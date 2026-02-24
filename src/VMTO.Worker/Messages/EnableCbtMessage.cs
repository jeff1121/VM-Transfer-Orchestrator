namespace VMTO.Worker.Messages;

public sealed record EnableCbtMessage(
    Guid JobId,
    Guid StepId,
    Guid SourceConnectionId,
    string VmId,
    Guid CorrelationId);
