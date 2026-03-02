namespace VMTO.Worker.Messages;

public sealed record JobStartedMessage(
    Guid JobId,
    List<string> StepNames,
    List<Guid> StepIds,
    Guid SourceConnectionId,
    Guid TargetConnectionId,
    string StorageEndpoint,
    string StorageBucket,
    Guid CorrelationId,
    string VmId = "",
    string DiskKey = "",
    string TargetFormat = "qcow2",
    string VmName = "",
    int Cores = 2,
    int MemoryMb = 2048);
