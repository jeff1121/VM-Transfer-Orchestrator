namespace VMTO.Infrastructure.Resilience;

public sealed class RetryPolicyOptions
{
    public int MaxRetryAttempts { get; set; } = 5;
    public double BaseDelaySeconds { get; set; } = 1;
    public double MaxDelaySeconds { get; set; } = 30;
    public double RequestTimeoutSeconds { get; set; } = 120;
    public bool UseJitter { get; set; } = true;
}
