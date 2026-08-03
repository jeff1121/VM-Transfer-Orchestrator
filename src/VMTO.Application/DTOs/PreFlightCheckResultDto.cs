namespace VMTO.Application.DTOs;

public sealed record PreFlightCheckItemDto(
    string Name,
    bool IsPassed,
    string Message,
    string? Details = null);

public sealed record PreFlightCheckResultDto(
    Guid ConnectionId,
    string VmId,
    bool IsAllPassed,
    IReadOnlyList<PreFlightCheckItemDto> Items);

public sealed record HyperVVmDiskInfoDto(
    string DiskKey,
    string Path,
    long SizeBytes,
    string Format);

public sealed record HyperVVmDetailsDto(
    string Id,
    string Name,
    string State,
    int CpuCount,
    long MemoryBytes,
    string GuestOs,
    int CheckpointCount,
    IReadOnlyList<HyperVVmDiskInfoDto> Disks);
