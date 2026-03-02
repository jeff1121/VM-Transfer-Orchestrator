namespace VMTO.Worker.Messages;

public sealed record StepCompletedMessage(
    Guid JobId,
    Guid StepId,
    string StepName,
    Guid CorrelationId,
    Dictionary<string, string>? OutputData = null);
