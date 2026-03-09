using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VMTO.Infrastructure.Resilience;

public sealed partial class ChaosPolicy(
    IOptionsMonitor<ChaosOptions> optionsMonitor,
    IHostEnvironment hostEnvironment,
    ILogger<ChaosPolicy> logger) : IChaosPolicy
{
    private readonly object _sync = new();
    private bool? _enabledOverride;
    private double? _failureRateOverride;
    private int? _maxDelayMsOverride;
    private double? _timeoutRateOverride;

    public async Task ApplyAsync(string operationName, CancellationToken ct = default)
    {
        var snapshot = GetSnapshot();
        if (!snapshot.EnvironmentAllowed || !snapshot.Enabled)
        {
            return;
        }

        if (snapshot.MaxDelayMs > 0)
        {
            var delayMs = Random.Shared.Next(0, snapshot.MaxDelayMs + 1);
            if (delayMs > 0)
            {
                LogChaosDelay(logger, operationName, delayMs);
                await Task.Delay(delayMs, ct);
            }
        }

        var roll = Random.Shared.NextDouble();
        if (roll < snapshot.TimeoutRate)
        {
            LogChaosTimeout(logger, operationName);
            throw new TimeoutException($"Chaos timeout injected: {operationName}");
        }

        if (roll < snapshot.TimeoutRate + snapshot.FailureRate)
        {
            LogChaosFailure(logger, operationName);
            throw new InvalidOperationException($"Chaos failure injected: {operationName}");
        }
    }

    public void Configure(bool enabled, double? failureRate = null, int? maxDelayMs = null, double? timeoutRate = null)
    {
        lock (_sync)
        {
            _enabledOverride = enabled;
            if (failureRate.HasValue) _failureRateOverride = ClampRate(failureRate.Value);
            if (maxDelayMs.HasValue) _maxDelayMsOverride = Math.Max(0, maxDelayMs.Value);
            if (timeoutRate.HasValue) _timeoutRateOverride = ClampRate(timeoutRate.Value);
        }
    }

    public ChaosSnapshot GetSnapshot()
    {
        var configured = optionsMonitor.CurrentValue ?? new ChaosOptions();
        lock (_sync)
        {
            return new ChaosSnapshot(
                Enabled: _enabledOverride ?? configured.Enabled,
                EnvironmentAllowed: hostEnvironment.IsDevelopment() || hostEnvironment.IsStaging(),
                FailureRate: _failureRateOverride ?? ClampRate(configured.FailureRate),
                MaxDelayMs: _maxDelayMsOverride ?? Math.Max(0, configured.MaxDelayMs),
                TimeoutRate: _timeoutRateOverride ?? ClampRate(configured.TimeoutRate));
        }
    }

    private static double ClampRate(double value) => Math.Clamp(value, 0d, 1d);

    [LoggerMessage(
        EventId = 9501,
        Level = LogLevel.Warning,
        Message = "Chaos delay injected for {OperationName}: {DelayMs}ms")]
    private static partial void LogChaosDelay(ILogger logger, string operationName, int delayMs);

    [LoggerMessage(
        EventId = 9502,
        Level = LogLevel.Warning,
        Message = "Chaos timeout injected for {OperationName}")]
    private static partial void LogChaosTimeout(ILogger logger, string operationName);

    [LoggerMessage(
        EventId = 9503,
        Level = LogLevel.Warning,
        Message = "Chaos failure injected for {OperationName}")]
    private static partial void LogChaosFailure(ILogger logger, string operationName);
}
