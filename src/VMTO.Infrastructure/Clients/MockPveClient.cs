using VMTO.Application.Ports.Services;
using VMTO.Shared;

namespace VMTO.Infrastructure.Clients;

public sealed class MockPveClient : IPveClient
{
    private int _nextVmId = 200;

    public Task<Result<int>> CreateVmAsync(Guid connectionId, string vmName, int cores, int memoryMb, CancellationToken ct = default)
    {
        return Task.FromResult(Result<int>.Success(_nextVmId++));
    }

    public Task<Result> ImportDiskAsync(Guid connectionId, int vmId, string storageUri, string diskFormat, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        progress?.Report(100);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> ConfigureVmAsync(Guid connectionId, int vmId, Dictionary<string, string> settings, CancellationToken ct = default)
    {
        return Task.FromResult(Result.Success());
    }
}
