namespace VMTO.Infrastructure.Resilience;

public sealed class ChaosOptions
{
    public bool Enabled { get; set; }
    public double FailureRate { get; set; }
    public int MaxDelayMs { get; set; }
    public double TimeoutRate { get; set; }
}

public sealed record ChaosSnapshot(
    bool Enabled,
    bool EnvironmentAllowed,
    double FailureRate,
    int MaxDelayMs,
    double TimeoutRate);
