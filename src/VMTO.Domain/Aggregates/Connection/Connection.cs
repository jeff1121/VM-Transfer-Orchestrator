using VMTO.Domain.ValueObjects;

namespace VMTO.Domain.Aggregates.Connection;

public sealed class Connection
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public PlatformKind Type { get; private set; }
    public string Endpoint { get; private set; }
    public EncryptedSecret EncryptedSecret { get; private set; }
    public string MetadataJson { get; private set; }
    public DateTime? ValidatedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Connection(
        string name,
        PlatformKind type,
        string endpoint,
        EncryptedSecret encryptedSecret,
        string? metadataJson = null)
    {
        Id = Guid.NewGuid();
        Name = name;
        Type = type;
        Endpoint = endpoint;
        EncryptedSecret = encryptedSecret;
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson;
        CreatedAt = DateTime.UtcNow;
    }

    private Connection()
    {
        Name = string.Empty;
        Endpoint = string.Empty;
        EncryptedSecret = null!;
        MetadataJson = "{}";
    }

    public void MarkValidated()
    {
        ValidatedAt = DateTime.UtcNow;
    }

    public void UpdateSecret(EncryptedSecret secret)
    {
        EncryptedSecret = secret;
        ValidatedAt = null;
    }

    public void UpdateMetadata(string metadataJson)
    {
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson;
    }
}
