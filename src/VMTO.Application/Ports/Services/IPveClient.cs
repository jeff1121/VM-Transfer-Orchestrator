using VMTO.Shared;

namespace VMTO.Application.Ports.Services;

public interface IPveClient
{
    Task<Result<int>> CreateVmAsync(Guid connectionId, string vmName, int cores, int memoryMb, CancellationToken ct = default);
    Task<Result> ImportDiskAsync(Guid connectionId, int vmId, string storageUri, string diskFormat, IProgress<int>? progress = null, CancellationToken ct = default);
    Task<Result> ConfigureVmAsync(Guid connectionId, int vmId, Dictionary<string, string> settings, CancellationToken ct = default);
}
