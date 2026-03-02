namespace VMTO.Domain.ValueObjects;

public sealed record EncryptedSecret(string CipherText, string? KeyId = null)
{
    public override string ToString() => "[REDACTED]";
}
