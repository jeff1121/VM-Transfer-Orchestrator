namespace VMTO.Worker.Messages;

public sealed record ExportVmdkMessage(
    Guid JobId,
    Guid StepId,
    Guid SourceConnectionId,
    string VmId,
    string DiskKey,
    Guid CorrelationId);
