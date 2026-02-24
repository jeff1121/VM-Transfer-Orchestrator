using Microsoft.AspNetCore.DataProtection;
using VMTO.Application.Ports.Services;
using VMTO.Domain.ValueObjects;

namespace VMTO.Infrastructure.Security;

public sealed class DataProtectionEncryptionService : IEncryptionService
{
    private const string Purpose = "VMTO.ConnectionSecrets";
    private readonly IDataProtector _protector;

    public DataProtectionEncryptionService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public EncryptedSecret Encrypt(string plainText)
    {
        var cipherText = _protector.Protect(plainText);
        return new EncryptedSecret(cipherText, Purpose);
    }

    public string Decrypt(EncryptedSecret secret)
    {
        return _protector.Unprotect(secret.CipherText);
    }
}
