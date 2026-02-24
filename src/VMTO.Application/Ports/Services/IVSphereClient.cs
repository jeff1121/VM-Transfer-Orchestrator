using VMTO.Application.DTOs;
using VMTO.Shared;

namespace VMTO.Application.Ports.Services;

public interface IVSphereClient
{
    Task<Result<IReadOnlyList<VmInfoDto>>> ListVmsAsync(Guid connectionId, CancellationToken ct = default);
    Task<Result<Stream>> ExportVmdkAsync(Guid connectionId, string vmId, string diskKey, IProgress<int>? progress = null, CancellationToken ct = default);
    Task<Result<bool>> IsCbtEnabledAsync(Guid connectionId, string vmId, CancellationToken ct = default);
    Task<Result> EnableCbtAsync(Guid connectionId, string vmId, CancellationToken ct = default);
}
