namespace VMTO.Worker;

public sealed class Worker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // MassTransit consumers handle work via message bus.
        // Hangfire handles scheduled/recurring jobs.
        // This hosted service is a placeholder for any additional startup logic.
        return Task.CompletedTask;
    }
}
