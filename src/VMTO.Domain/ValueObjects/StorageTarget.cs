namespace VMTO.Domain.ValueObjects;

public sealed record StorageTarget
{
    public StorageType Type { get; }
    public string Endpoint { get; }
    public string BucketOrPath { get; }
    public string? Region { get; }

    public StorageTarget(StorageType type, string endpoint, string bucketOrPath, string? region = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(endpoint, nameof(endpoint));
        ArgumentException.ThrowIfNullOrEmpty(bucketOrPath, nameof(bucketOrPath));
        Type = type;
        Endpoint = endpoint;
        BucketOrPath = bucketOrPath;
        Region = region;
    }
}
