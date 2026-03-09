namespace VMTO.Infrastructure.Resilience;

public interface IChaosPolicy
{
    Task ApplyAsync(string operationName, CancellationToken ct = default);
    void Configure(bool enabled, double? failureRate = null, int? maxDelayMs = null, double? timeoutRate = null);
    ChaosSnapshot GetSnapshot();
}
