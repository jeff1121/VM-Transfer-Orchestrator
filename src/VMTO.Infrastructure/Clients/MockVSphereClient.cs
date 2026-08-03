using VMTO.Application.DTOs;
using VMTO.Application.Ports.Services;
using VMTO.Shared;

namespace VMTO.Infrastructure.Clients;

public sealed class MockVSphereClient : IVSphereClient
{
    public Task<Result<IReadOnlyList<VmInfoDto>>> ListVmsAsync(Guid connectionId, CancellationToken ct = default)
    {
        IReadOnlyList<VmInfoDto> vms =
        [
            new VmInfoDto("vm-101", "web-server-01", 4, 8L * 1024 * 1024 * 1024, ["disk-0"]),
            new VmInfoDto("vm-102", "db-server-01", 8, 16L * 1024 * 1024 * 1024, ["disk-0", "disk-1"]),
            new VmInfoDto("vm-103", "app-server-01", 2, 4L * 1024 * 1024 * 1024, ["disk-0"]),
        ];
        return Task.FromResult(Result<IReadOnlyList<VmInfoDto>>.Success(vms));
    }

    public Task<Result<Stream>> ExportVmdkAsync(Guid connectionId, string vmId, string diskKey, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var stream = new MemoryStream(new byte[1024]);
        progress?.Report(100);
        return Task.FromResult(Result<Stream>.Success((Stream)stream));
    }

    public Task<Result<bool>> IsCbtEnabledAsync(Guid connectionId, string vmId, CancellationToken ct = default)
    {
        return Task.FromResult(Result<bool>.Success(true));
    }

    public Task<Result> EnableCbtAsync(Guid connectionId, string vmId, CancellationToken ct = default)
    {
        return Task.FromResult(Result.Success());
    }

    public Task<Result<string>> GetVmStateAsync(Guid connectionId, string vmId, CancellationToken ct = default)
    {
        return Task.FromResult(Result<string>.Success("poweredOff"));
    }

    public Task<Result<HyperVVmDetailsDto>> GetVmDetailsAsync(Guid connectionId, string vmId, CancellationToken ct = default)
    {
        var details = new HyperVVmDetailsDto(vmId, "web-server-01", "poweredOff", 4, 8192, "VMware ESXi Guest", 0, []);
        return Task.FromResult(Result<HyperVVmDetailsDto>.Success(details));
    }

    public Task<Result<PreFlightCheckResultDto>> RunPreFlightCheckAsync(Guid connectionId, string vmId, CancellationToken ct = default)
    {
        IReadOnlyList<PreFlightCheckItemDto> items =
        [
            new PreFlightCheckItemDto("vCenterReachability", true, "vCenter API is reachable.")
        ];
        return Task.FromResult(Result<PreFlightCheckResultDto>.Success(new PreFlightCheckResultDto(connectionId, vmId, true, items)));
    }

    public Task<Result<Stream>> ExportDiskAsync(Guid connectionId, string vmId, string diskKey, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        return ExportVmdkAsync(connectionId, vmId, diskKey, progress, ct);
    }
}
