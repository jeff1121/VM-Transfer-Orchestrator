using Microsoft.AspNetCore.SignalR;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Enums;
using VMTO.Infrastructure.Hubs;

namespace VMTO.Infrastructure.Notifications;

public sealed class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<MigrationHub> _hubContext;

    public SignalRNotificationService(IHubContext<MigrationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendJobProgressAsync(Guid jobId, int progress, JobStatus status, CancellationToken ct = default)
    {
        await _hubContext.Clients.All.SendAsync(
            "JobProgress",
            new { jobId, progress, status = status.ToString() },
            ct);
    }

    public async Task SendStepProgressAsync(Guid jobId, Guid stepId, int progress, StepStatus status, CancellationToken ct = default)
    {
        await _hubContext.Clients.All.SendAsync(
            "StepProgress",
            new { jobId, stepId, progress, status = status.ToString() },
            ct);
    }
}
