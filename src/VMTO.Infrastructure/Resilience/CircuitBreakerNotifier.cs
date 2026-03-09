using Microsoft.Extensions.Logging;
using VMTO.Application.Ports.Services;
using VMTO.Shared.Telemetry;

namespace VMTO.Infrastructure.Resilience;

public sealed partial class CircuitBreakerNotifier(
    IWebhookService webhookService,
    ILogger<CircuitBreakerNotifier> logger)
{
    public async ValueTask NotifyStateChangedAsync(string serviceName, string state, Exception? exception = null, CancellationToken ct = default)
    {
        VmtoMetrics.SetCircuitBreakerState(serviceName, MapState(state));
        LogStateChanged(logger, serviceName, state, exception?.Message);

        // 重用既有 Webhook 事件通道，避免引入新事件契約造成破壞性變更。
        await webhookService.NotifyStepFailedAsync(
            Guid.Empty,
            Guid.Empty,
            $"CircuitBreaker:{serviceName}",
            exception is null ? $"state={state}" : $"state={state}, error={exception.Message}",
            ct);
    }

    private static int MapState(string state) => state switch
    {
        "open" => 2,
        "half_open" => 1,
        _ => 0
    };

    public ValueTask NotifyRetryAsync(string serviceName, int attempt, TimeSpan delay, Exception? exception = null)
    {
        LogRetry(logger, serviceName, attempt, delay.TotalSeconds, exception?.Message);
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(
        EventId = 9201,
        Level = LogLevel.Warning,
        Message = "Circuit breaker {ServiceName} state changed to {State}. Error: {ErrorMessage}")]
    private static partial void LogStateChanged(ILogger logger, string serviceName, string state, string? errorMessage);

    [LoggerMessage(
        EventId = 9202,
        Level = LogLevel.Warning,
        Message = "Retrying {ServiceName} request. Attempt={Attempt}, DelaySeconds={DelaySeconds}, Error={ErrorMessage}")]
    private static partial void LogRetry(ILogger logger, string serviceName, int attempt, double delaySeconds, string? errorMessage);
}
