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
        progress?.Report(100);
        return Task.FromResult(Result<Stream>.Success((Stream)new MemoryStream(new byte[1024])));
    }

    public Task<Result<bool>> IsCbtEnabledAsync(Guid connectionId, string vmId, CancellationToken ct = default)
        => Task.FromResult(Result<bool>.Success(true));

    public Task<Result> EnableCbtAsync(Guid connectionId, string vmId, CancellationToken ct = default)
        => Task.FromResult(Result.Success());

    public Task<Result<string>> GetVmStateAsync(Guid connectionId, string vmId, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Success("poweredOff"));

    public Task<Result<VmInspectionDto>> InspectAsync(Guid connectionId, string vmId, CancellationToken ct = default)
    {
        var inspection = new VmInspectionDto(
            vmId, "web-server-01", "poweredOff", 4, 8192, "VMware ESXi Guest", 0, false,
            [new DiskDescriptorDto("disk-0", "[datastore] vm/disk-0.vmdk", 20L * 1024 * 1024 * 1024, "VMDK")]);
        return Task.FromResult(Result<VmInspectionDto>.Success(inspection));
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
        => ExportVmdkAsync(connectionId, vmId, diskKey, progress, ct);

    public Task<Result> CleanupExportAsync(Guid connectionId, string vmId, CancellationToken ct = default)
        => Task.FromResult(Result.Success());
}
