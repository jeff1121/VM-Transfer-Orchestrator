namespace VMTO.Worker.Messages;

public sealed record UploadArtifactMessage(
    Guid JobId,
    Guid StepId,
    string LocalPath,
    string StorageKey,
    Guid CorrelationId);
