using VMTO.Application.DTOs;
using VMTO.Shared;

namespace VMTO.Application.Ports.Services;

public interface IHyperVClient : ISourcePlatformPort
{
    Task<Result<Stream>> ExportVhdxAsync(Guid connectionId, string vmId, string diskKey, IProgress<int>? progress = null, CancellationToken ct = default);
}
