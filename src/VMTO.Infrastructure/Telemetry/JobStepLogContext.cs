using System.Threading;

namespace VMTO.Infrastructure.Telemetry;

public static class JobStepLogContext
{
    private static readonly AsyncLocal<string?> JobIdStore = new();
    private static readonly AsyncLocal<string?> StepIdStore = new();

    public static string? CurrentJobId => JobIdStore.Value;
    public static string? CurrentStepId => StepIdStore.Value;

    public static IDisposable BeginScope(Guid jobId, Guid stepId)
    {
        var previousJobId = JobIdStore.Value;
        var previousStepId = StepIdStore.Value;

        JobIdStore.Value = jobId.ToString("D");
        StepIdStore.Value = stepId.ToString("D");
        return new Scope(previousJobId, previousStepId);
    }

    private sealed class Scope(string? previousJobId, string? previousStepId) : IDisposable
    {
        public void Dispose()
        {
            JobIdStore.Value = previousJobId;
            StepIdStore.Value = previousStepId;
        }
    }
}
