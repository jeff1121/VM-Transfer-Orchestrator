namespace VMTO.Application.DTOs;

public sealed record VmInfoDto(
    string Id,
    string Name,
    int CpuCount,
    long MemoryBytes,
    IReadOnlyList<string> DiskKeys);
