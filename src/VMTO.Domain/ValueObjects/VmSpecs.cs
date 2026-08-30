namespace VMTO.Domain.ValueObjects;

public sealed record DiskDescriptor(
    string DiskKey,
    string Format,
    long SizeBytes,
    string? Path = null);

public sealed record NetworkSpec(
    string Model,
    string? Bridge = null);

public sealed record VmHardwareSpec(
    int CpuCount,
    long MemoryBytes,
    string Firmware,
    bool SecureBoot,
    IReadOnlyList<DiskDescriptor> Disks,
    IReadOnlyList<NetworkSpec> Nics);

public sealed record ExportManifest(
    string VmId,
    IReadOnlyList<DiskDescriptor> Disks,
    string ChecksumAlgorithm);
