using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using VMTO.Shared.Telemetry;

namespace VMTO.Infrastructure.Resilience;

public static class CircuitBreakerPipelineFactory
{
    public static ResiliencePipeline Create(
        string serviceName,
        int minimumThroughput,
        TimeSpan breakDuration,
        RetryPolicyOptions retryOptions,
        Func<Exception, bool> retryClassifier,
        CircuitBreakerNotifier notifier)
    {
        VmtoMetrics.SetCircuitBreakerState(serviceName, 0);
        return new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(retryOptions.RequestTimeoutSeconds))
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = retryOptions.MaxRetryAttempts,
                Delay = TimeSpan.FromSeconds(retryOptions.BaseDelaySeconds),
                MaxDelay = TimeSpan.FromSeconds(retryOptions.MaxDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = retryOptions.UseJitter,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(retryClassifier),
                OnRetry = args => notifier.NotifyRetryAsync(
                    serviceName,
                    args.AttemptNumber + 1,
                    args.RetryDelay,
                    args.Outcome.Exception)
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 1.0,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = minimumThroughput,
                BreakDuration = breakDuration,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(retryClassifier),
                OnOpened = _ => notifier.NotifyStateChangedAsync(serviceName, "open"),
                OnClosed = _ => notifier.NotifyStateChangedAsync(serviceName, "closed"),
                OnHalfOpened = _ => notifier.NotifyStateChangedAsync(serviceName, "half_open")
            })
            .Build();
    }
}
