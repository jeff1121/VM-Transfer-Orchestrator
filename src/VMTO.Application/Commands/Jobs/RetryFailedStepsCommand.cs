namespace VMTO.Application.Commands.Jobs;

public sealed record RetryFailedStepsCommand(Guid JobId);
