namespace VMTO.Worker;

public sealed partial class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        LogStopping(logger);
        await base.StopAsync(cancellationToken);
        LogStopped(logger);
    }

    [LoggerMessage(EventId = 9401, Level = LogLevel.Information, Message = "Worker service started.")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(EventId = 9402, Level = LogLevel.Information, Message = "Worker is shutting down: stop accepting new messages and wait for in-flight tasks.")]
    private static partial void LogStopping(ILogger logger);

    [LoggerMessage(EventId = 9403, Level = LogLevel.Information, Message = "Worker shutdown completed.")]
    private static partial void LogStopped(ILogger logger);
}
