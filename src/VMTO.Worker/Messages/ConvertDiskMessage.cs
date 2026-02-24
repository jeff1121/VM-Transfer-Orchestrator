namespace VMTO.Worker.Messages;

public sealed record ConvertDiskMessage(
    Guid JobId,
    Guid StepId,
    string InputStorageKey,
    string OutputStorageKey,
    string TargetFormat,
    Guid CorrelationId);
