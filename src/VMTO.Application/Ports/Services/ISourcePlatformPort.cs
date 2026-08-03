using VMTO.Application.DTOs;
using VMTO.Shared;

namespace VMTO.Application.Ports.Services;

public interface ISourcePlatformPort
{
    Task<Result<IReadOnlyList<VmInfoDto>>> ListVmsAsync(Guid connectionId, CancellationToken ct = default);
    Task<Result<string>> GetVmStateAsync(Guid connectionId, string vmId, CancellationToken ct = default);
    Task<Result<HyperVVmDetailsDto>> GetVmDetailsAsync(Guid connectionId, string vmId, CancellationToken ct = default);
    Task<Result<PreFlightCheckResultDto>> RunPreFlightCheckAsync(Guid connectionId, string vmId, CancellationToken ct = default);
    Task<Result<Stream>> ExportDiskAsync(Guid connectionId, string vmId, string diskKey, IProgress<int>? progress = null, CancellationToken ct = default);
}

public interface ITargetPlatformPort
{
    Task<Result<int>> CreateVmAsync(Guid connectionId, string name, int cores, int memoryMb, CancellationToken ct = default);
    Task<Result> ImportDiskAsync(Guid connectionId, int vmId, string storageKey, string format, IProgress<int>? progress = null, CancellationToken ct = default);
    Task<Result<string>> GetVmStatusAsync(Guid connectionId, string vmId, CancellationToken ct = default);
}
