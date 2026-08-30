namespace VMTO.Worker.Messages;

public sealed record ExportDiskMessage(
    Guid JobId,
    Guid StepId,
    Guid SourceConnectionId,
    string VmId,
    string DiskKey,
    Guid CorrelationId);

public sealed record ProvisionTargetVmMessage(
    Guid JobId,
    Guid StepId,
    Guid TargetConnectionId,
    string VmName,
    int Cores,
    int MemoryMb,
    Guid CorrelationId);

public sealed record AttachDiskMessage(
    Guid JobId,
    Guid StepId,
    Guid TargetConnectionId,
    string TargetVmId,
    string StorageUri,
    string DiskFormat,
    string DiskKey,
    Guid CorrelationId);

public sealed record ConfigureTargetVmMessage(
    Guid JobId,
    Guid StepId,
    Guid TargetConnectionId,
    string TargetVmId,
    Guid CorrelationId);

public sealed record CleanupMessage(
    Guid JobId,
    Guid StepId,
    Guid SourceConnectionId,
    string VmId,
    Guid CorrelationId);

public sealed record TargetRollbackMessage(
    Guid JobId,
    Guid TargetConnectionId,
    string? TargetVmId,
    Guid CorrelationId);
