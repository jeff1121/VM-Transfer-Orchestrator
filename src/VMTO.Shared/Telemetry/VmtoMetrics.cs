using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace VMTO.Shared.Telemetry;

public static class VmtoMetrics
{
    private static readonly Meter Meter = new(ActivitySources.Name, ActivitySources.Version);

    private static readonly Counter<long> JobsTotalCounter = Meter.CreateCounter<long>("vmto_jobs_total");
    private static readonly Histogram<double> StepDurationHistogram = Meter.CreateHistogram<double>("vmto_step_duration_seconds", unit: "s");
    private static readonly Counter<long> TransferBytesCounter = Meter.CreateCounter<long>("vmto_transfer_bytes_total", unit: "By");
    private static readonly ConcurrentDictionary<Guid, int> ActiveJobSteps = new();
    private static readonly ConcurrentDictionary<string, long> QueueDepth = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, int> CircuitBreakerStates = new(StringComparer.OrdinalIgnoreCase);
    private static long _activeJobs;

    static VmtoMetrics()
    {
        _ = Meter.CreateObservableGauge<long>("vmto_active_jobs", () => Volatile.Read(ref _activeJobs));
        _ = Meter.CreateObservableGauge<long>("vmto_queue_depth", ObserveQueueDepth);
        _ = Meter.CreateObservableGauge<int>("vmto_circuit_breaker_state", ObserveCircuitBreakerState);
    }

    public static void RecordJob(string status, string strategy)
    {
        JobsTotalCounter.Add(1, new KeyValuePair<string, object?>("status", status), new KeyValuePair<string, object?>("strategy", strategy));
    }

    public static void RecordStepDuration(string stepName, string status, double seconds)
    {
        StepDurationHistogram.Record(seconds, new KeyValuePair<string, object?>("step_name", stepName), new KeyValuePair<string, object?>("status", status));
    }

    public static void AddTransferBytes(long bytes)
    {
        if (bytes > 0)
        {
            TransferBytesCounter.Add(bytes);
        }
    }

    public static void TrackJobStepStarted(Guid jobId)
    {
        var count = ActiveJobSteps.AddOrUpdate(jobId, 1, (_, current) => current + 1);
        if (count == 1)
        {
            Interlocked.Increment(ref _activeJobs);
        }
    }

    public static void TrackJobStepEnded(Guid jobId)
    {
        if (!ActiveJobSteps.TryGetValue(jobId, out var current))
        {
            return;
        }

        if (current <= 1)
        {
            ActiveJobSteps.TryRemove(jobId, out _);
            Interlocked.Decrement(ref _activeJobs);
            return;
        }

        ActiveJobSteps.TryUpdate(jobId, current - 1, current);
    }

    public static void IncrementQueueDepth(string queueName)
    {
        QueueDepth.AddOrUpdate(queueName, 1, (_, current) => current + 1);
    }

    public static void DecrementQueueDepth(string queueName)
    {
        QueueDepth.AddOrUpdate(queueName, 0, (_, current) => Math.Max(0, current - 1));
    }

    public static void SetCircuitBreakerState(string serviceName, int state)
    {
        CircuitBreakerStates.AddOrUpdate(serviceName, state, (_, _) => state);
    }

    private static IEnumerable<Measurement<long>> ObserveQueueDepth()
    {
        foreach (var item in QueueDepth)
        {
            yield return new Measurement<long>(item.Value, new KeyValuePair<string, object?>("queue_name", item.Key));
        }
    }

    private static IEnumerable<Measurement<int>> ObserveCircuitBreakerState()
    {
        foreach (var item in CircuitBreakerStates)
        {
            yield return new Measurement<int>(item.Value, new KeyValuePair<string, object?>("service_name", item.Key));
        }
    }
}
