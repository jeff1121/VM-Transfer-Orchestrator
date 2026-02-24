namespace VMTO.Domain.ValueObjects;

public sealed record StorageTarget(
    StorageType Type,
    string Endpoint,
    string BucketOrPath,
    string? Region = null);
