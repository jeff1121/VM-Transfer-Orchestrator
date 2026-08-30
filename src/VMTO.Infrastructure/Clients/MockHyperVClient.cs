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
        => Task.FromResult(Result<string>.Success("Off"));

    public Task<Result<VmInspectionDto>> InspectAsync(Guid connectionId, string vmId, CancellationToken ct = default)
    {
        var details = new VmInspectionDto(
            vmId,
            vmId == "hv-vm-02" ? "win-server-02" : "win-server-01",
            "Off",
            vmId == "hv-vm-02" ? 8 : 4,
            (vmId == "hv-vm-02" ? 16L : 8L) * 1024 * 1024 * 1024,
            "Windows Server 2022 Datacenter",
            0,
            false,
            [new DiskDescriptorDto("disk-0", @"C:\HyperV\Virtual Hard Disks\disk-0.vhdx", 50L * 1024 * 1024 * 1024, "VHDX")]);
        if (vmId == "hv-vm-02")
        {
            details = details with
            {
                Disks =
                [
                    new DiskDescriptorDto("disk-0", @"C:\HyperV\Virtual Hard Disks\disk-0.vhdx", 50L * 1024 * 1024 * 1024, "VHDX"),
                    new DiskDescriptorDto("disk-1", @"C:\HyperV\Virtual Hard Disks\disk-1.vhdx", 100L * 1024 * 1024 * 1024, "VHDX")
                ]
            };
        }

        return Task.FromResult(Result<VmInspectionDto>.Success(details));
    }

    public Task<Result<PreFlightCheckResultDto>> RunPreFlightCheckAsync(Guid connectionId, string vmId, CancellationToken ct = default)
    {
        IReadOnlyList<PreFlightCheckItemDto> items =
        [
            new PreFlightCheckItemDto("ApiReachability", true, "Hyper-V API endpoint is accessible."),
            new PreFlightCheckItemDto("VmExistenceAndState", true, "VM is powered off and ready for offline export.", "Current State: Off"),
            new PreFlightCheckItemDto("DiskAccess", true, "VHDX disk files are accessible and read permissions verified."),
            new PreFlightCheckItemDto("StorageSpace", true, "Target staging storage space verified.", "Available: 500GB, Required: 50GB"),
            new PreFlightCheckItemDto("StandaloneHost", true, "Host is not a failover cluster node.")
        ];
        return Task.FromResult(Result<PreFlightCheckResultDto>.Success(new PreFlightCheckResultDto(connectionId, vmId, true, items)));
    }

    public Task<Result<Stream>> ExportDiskAsync(Guid connectionId, string vmId, string diskKey, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        progress?.Report(100);
        return Task.FromResult(Result<Stream>.Success((Stream)new MemoryStream(new byte[1024])));
    }

    public Task<Result> CleanupExportAsync(Guid connectionId, string vmId, CancellationToken ct = default)
        => Task.FromResult(Result.Success());
}
