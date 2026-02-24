using VMTO.Domain.ValueObjects;

namespace VMTO.Application.Ports.Services;

public interface IEncryptionService
{
    EncryptedSecret Encrypt(string plainText);
    string Decrypt(EncryptedSecret secret);
}
