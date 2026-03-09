namespace VMTO.Infrastructure.Resilience;

public sealed class ChaosDecorator<T>(T inner, IChaosPolicy chaosPolicy)
    where T : class
{
    public async Task<TResult> ExecuteAsync<TResult>(
        string operationName,
        Func<T, CancellationToken, Task<TResult>> action,
        CancellationToken ct = default)
    {
        await chaosPolicy.ApplyAsync(operationName, ct);
        return await action(inner, ct);
    }
}
