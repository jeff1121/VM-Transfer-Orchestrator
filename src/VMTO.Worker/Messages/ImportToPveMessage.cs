namespace VMTO.Worker.Messages;

public sealed record ImportToPveMessage(
    Guid JobId,
    Guid StepId,
    Guid TargetConnectionId,
    string StorageUri,
    string DiskFormat,
    string VmName,
    int Cores,
    int MemoryMb,
    Guid CorrelationId);
