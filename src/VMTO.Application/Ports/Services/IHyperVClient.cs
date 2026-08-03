using VMTO.Application.DTOs;
using VMTO.Shared;

namespace VMTO.Application.Ports.Services;

public interface IHyperVClient
{
    Task<Result<IReadOnlyList<VmInfoDto>>> ListVmsAsync(Guid connectionId, CancellationToken ct = default);
    Task<Result<string>> GetVmStateAsync(Guid connectionId, string vmId, CancellationToken ct = default);
    Task<Result<HyperVVmDetailsDto>> GetVmDetailsAsync(Guid connectionId, string vmId, CancellationToken ct = default);
    Task<Result<PreFlightCheckResultDto>> RunPreFlightCheckAsync(Guid connectionId, string vmId, CancellationToken ct = default);
    Task<Result<Stream>> ExportVhdxAsync(Guid connectionId, string vmId, string diskKey, IProgress<int>? progress = null, CancellationToken ct = default);
}
