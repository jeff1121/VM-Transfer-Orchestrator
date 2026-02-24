using VMTO.Domain.Enums;

namespace VMTO.Application.Ports.Services;

public interface INotificationService
{
    Task SendJobProgressAsync(Guid jobId, int progress, JobStatus status, CancellationToken ct = default);
    Task SendStepProgressAsync(Guid jobId, Guid stepId, int progress, StepStatus status, CancellationToken ct = default);
}
