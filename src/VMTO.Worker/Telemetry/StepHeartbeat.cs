using Microsoft.Extensions.Logging;

namespace VMTO.Worker.Telemetry;

public static class StepHeartbeat
{
    public static StepHeartbeatScope Start(
        Func<CancellationToken, Task> heartbeatAction,
        TimeSpan interval,
        ILogger logger,
        string consumerName,
        CancellationToken cancellationToken)
        => new(heartbeatAction, interval, logger, consumerName, cancellationToken);
}

public sealed partial class StepHeartbeatScope : IAsyncDisposable
{
    private readonly PeriodicTimer _timer;
    private readonly CancellationTokenSource _cts;
    private readonly Task _loopTask;
    private readonly Func<CancellationToken, Task> _heartbeatAction;
    private readonly ILogger _logger;
    private readonly string _consumerName;
    private bool _disposed;

    public StepHeartbeatScope(
        Func<CancellationToken, Task> heartbeatAction,
        TimeSpan interval,
        ILogger logger,
        string consumerName,
        CancellationToken cancellationToken)
    {
        _heartbeatAction = heartbeatAction;
        _logger = logger;
        _consumerName = consumerName;
        _timer = new PeriodicTimer(interval);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = RunAsync();
    }

    private async Task RunAsync()
    {
        while (await _timer.WaitForNextTickAsync(_cts.Token))
        {
            try
            {
                await _heartbeatAction(_cts.Token);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogHeartbeatFailed(_logger, ex, _consumerName);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _timer.Dispose();

        try
        {
            await _loopTask;
        }
        catch (OperationCanceledException)
        {
            // expected when disposing
        }

        _cts.Dispose();
    }

    [LoggerMessage(
        EventId = 9302,
        Level = LogLevel.Warning,
        Message = "Heartbeat failed in {ConsumerName}")]
    private static partial void LogHeartbeatFailed(ILogger logger, Exception exception, string consumerName);
}
