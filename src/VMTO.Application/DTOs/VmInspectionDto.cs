namespace VMTO.Application.DTOs;

public sealed record DiskDescriptorDto(
    string DiskKey,
    string Path,
    long SizeBytes,
    string Format);

public sealed record VmInspectionDto(
    string Id,
    string Name,
    string State,
    int CpuCount,
    long MemoryBytes,
    string? GuestOs,
    int CheckpointCount,
    bool IsClustered,
    IReadOnlyList<DiskDescriptorDto> Disks);
