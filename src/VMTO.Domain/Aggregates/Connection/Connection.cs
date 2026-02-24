using VMTO.Domain.ValueObjects;

namespace VMTO.Domain.Aggregates.Connection;

public sealed class Connection
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public ConnectionType Type { get; private set; }
    public string Endpoint { get; private set; }
    public EncryptedSecret EncryptedSecret { get; private set; }
    public DateTime? ValidatedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Connection(string name, ConnectionType type, string endpoint, EncryptedSecret encryptedSecret)
    {
        Id = Guid.NewGuid();
        Name = name;
        Type = type;
        Endpoint = endpoint;
        EncryptedSecret = encryptedSecret;
        CreatedAt = DateTime.UtcNow;
    }

    // EF Core / serialization
    private Connection()
    {
        Name = string.Empty;
        Endpoint = string.Empty;
        EncryptedSecret = null!;
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
}
