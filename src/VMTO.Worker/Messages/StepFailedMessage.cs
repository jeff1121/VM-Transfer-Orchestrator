namespace VMTO.Worker.Messages;

public sealed record StepFailedMessage(
    Guid JobId,
    Guid StepId,
    string StepName,
    string Error,
    Guid CorrelationId);
