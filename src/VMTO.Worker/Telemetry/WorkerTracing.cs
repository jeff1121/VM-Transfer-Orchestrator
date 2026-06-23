using System.Diagnostics;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Infrastructure.Telemetry;
using VMTO.Shared.Telemetry;

namespace VMTO.Worker.Telemetry;

public static class WorkerTracing
{
    public static WorkerStepTelemetryScope StartStepActivity(string consumerName, JobStep step, Guid jobId, Guid stepId, Guid correlationId)
    {
        var activity = ActivitySources.Default.StartActivity($"worker.{consumerName}", ActivityKind.Consumer);
        activity?.SetTag("vmto.consumer", consumerName);
        activity?.SetTag("vmto.step.name", step.Name);
        activity?.SetTag("vmto.job.id", jobId.ToString());
        activity?.SetTag("vmto.step.id", stepId.ToString());
        activity?.SetTag("vmto.correlation.id", correlationId.ToString());
        return new WorkerStepTelemetryScope(activity, consumerName, step, jobId);
    }
}

public sealed class WorkerStepTelemetryScope : IDisposable
{
    private readonly Activity? _activity;
    private readonly string _consumerName;
    private readonly JobStep _step;
    private readonly Guid _jobId;
    private readonly IDisposable _logScope;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private bool _disposed;

    public WorkerStepTelemetryScope(Activity? activity, string consumerName, JobStep step, Guid jobId)
    {
        _activity = activity;
        _consumerName = consumerName;
        _step = step;
        _jobId = jobId;
        _logScope = JobStepLogContext.BeginScope(jobId, step.Id);

        VmtoMetrics.TrackJobStepStarted(jobId);
        VmtoMetrics.IncrementQueueDepth(consumerName);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopwatch.Stop();

        var status = _step.Status.ToString().ToLowerInvariant();
        VmtoMetrics.RecordStepDuration(_step.Name, status, _stopwatch.Elapsed.TotalSeconds);
        VmtoMetrics.TrackJobStepEnded(_jobId);
        VmtoMetrics.DecrementQueueDepth(_consumerName);

        _activity?.SetTag("vmto.step.status", status);
        _activity?.SetTag("vmto.step.duration_seconds", _stopwatch.Elapsed.TotalSeconds);
        _activity?.Dispose();
        _logScope.Dispose();
    }
}
