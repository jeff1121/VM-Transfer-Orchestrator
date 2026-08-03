using VMTO.Application.Ports.Services;
using VMTO.Shared;

namespace VMTO.Infrastructure.Clients;

public sealed class MockPveClient : IPveClient
{
    private int _nextVmId = 200;

    public Task<Result<int>> CreateVmAsync(Guid connectionId, string name, int cores, int memoryMb, CancellationToken ct = default)
    {
        return Task.FromResult(Result<int>.Success(Interlocked.Increment(ref _nextVmId)));
    }

    public Task<Result> ImportDiskAsync(Guid connectionId, int vmId, string storageKey, string format, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        _nextVmId++;
        progress?.Report(100);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> ImportDiskAsync(Guid connectionId, int vmId, string storageKey, string format, CancellationToken ct = default)
    {
        return ImportDiskAsync(connectionId, vmId, storageKey, format, (IProgress<int>?)null, ct);
    }

    public Task<Result> ConfigureVmAsync(Guid connectionId, int vmId, Dictionary<string, string> settings, CancellationToken ct = default)
    {
        return Task.FromResult(Result.Success());
    }

    public Task<Result<string>> GetVmStatusAsync(Guid connectionId, string vmId, CancellationToken ct = default)
    {
        return Task.FromResult(Result<string>.Success("running"));
    }
}
