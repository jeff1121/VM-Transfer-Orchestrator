namespace VMTO.Worker.Messages;

public sealed record VerifyMessage(
    Guid JobId,
    Guid StepId,
    Guid ArtifactId,
    string ExpectedChecksum,
    Guid CorrelationId);
