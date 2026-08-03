using VMTO.Application.DTOs;
using VMTO.Application.Ports.Services;
using VMTO.Shared;

namespace VMTO.Infrastructure.Clients;

public sealed class MockHyperVClient : IHyperVClient
{
    public Task<Result<IReadOnlyList<VmInfoDto>>> ListVmsAsync(Guid connectionId, CancellationToken ct = default)
    {
        IReadOnlyList<VmInfoDto> vms =
        [
            new VmInfoDto("hv-vm-01", "win-server-01", 4, 8L * 1024 * 1024 * 1024, ["disk-0"]),
            new VmInfoDto("hv-vm-02", "win-server-02", 8, 16L * 1024 * 1024 * 1024, ["disk-0", "disk-1"]),
        ];
        return Task.FromResult(Result<IReadOnlyList<VmInfoDto>>.Success(vms));
    }

    public Task<Result<string>> GetVmStateAsync(Guid connectionId, string vmId, CancellationToken ct = default)
    {
        // Mock state: Off for testing offline export
        return Task.FromResult(Result<string>.Success("Off"));
    }

    public Task<Result<Stream>> ExportVhdxAsync(Guid connectionId, string vmId, string diskKey, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var stream = new MemoryStream(new byte[1024]);
        progress?.Report(100);
        return Task.FromResult(Result<Stream>.Success((Stream)stream));
    }
}
