using VMTO.Application.DTOs;
using VMTO.Shared;

namespace VMTO.Application.Ports.Services;

public interface IVmSourcePort
{
    Task<Result<IReadOnlyList<VmInfoDto>>> ListVmsAsync(Guid connectionId, CancellationToken ct = default);
    Task<Result<string>> GetVmStateAsync(Guid connectionId, string vmId, CancellationToken ct = default);
    Task<Result<VmInspectionDto>> InspectAsync(Guid connectionId, string vmId, CancellationToken ct = default);
    Task<Result<PreFlightCheckResultDto>> RunPreFlightCheckAsync(Guid connectionId, string vmId, CancellationToken ct = default);
    Task<Result<Stream>> ExportDiskAsync(Guid connectionId, string vmId, string diskKey, IProgress<int>? progress = null, CancellationToken ct = default);
    Task<Result> CleanupExportAsync(Guid connectionId, string vmId, CancellationToken ct = default);
}

public interface IVmTargetPort
{
    Task<Result<int>> CreateVmAsync(Guid connectionId, string name, int cores, int memoryMb, CancellationToken ct = default);
    Task<Result> ImportDiskAsync(Guid connectionId, int vmId, string storageKey, string format, IProgress<int>? progress = null, CancellationToken ct = default);
    Task<Result> ConfigureVmAsync(Guid connectionId, int vmId, Dictionary<string, string> settings, CancellationToken ct = default);
    Task<Result<string>> GetVmStatusAsync(Guid connectionId, string vmId, CancellationToken ct = default);
    Task<Result> RollbackAsync(Guid connectionId, string? targetVmId, string idempotencyKey, CancellationToken ct = default);
}

public interface ISourcePlatformPort : IVmSourcePort;
public interface ITargetPlatformPort : IVmTargetPort;
