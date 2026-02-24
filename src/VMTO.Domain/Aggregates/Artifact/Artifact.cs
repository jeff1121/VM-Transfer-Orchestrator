using VMTO.Domain.ValueObjects;

namespace VMTO.Domain.Aggregates.Artifact;

public sealed class Artifact
{
    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public string FileName { get; private set; }
    public ArtifactFormat Format { get; private set; }
    public Checksum Checksum { get; private set; }
    public long SizeBytes { get; private set; }
    public string StorageUri { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Artifact(Guid jobId, string fileName, ArtifactFormat format, Checksum checksum, long sizeBytes, string storageUri)
    {
        Id = Guid.NewGuid();
        JobId = jobId;
        FileName = fileName;
        Format = format;
        Checksum = checksum;
        SizeBytes = sizeBytes;
        StorageUri = storageUri;
        CreatedAt = DateTime.UtcNow;
    }

    // EF Core / serialization
    private Artifact()
    {
        FileName = string.Empty;
        Checksum = null!;
        StorageUri = string.Empty;
    }
}
