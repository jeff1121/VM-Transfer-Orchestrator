using System.Security.Cryptography;
using System.Text;
using VMTO.Domain.Aggregates.License;
using VMTO.Shared;

namespace VMTO.Domain.Licensing;

public sealed record DecodedLicenseKey(
    int Version,
    LicensePlan Plan,
    int MaxConcurrentJobs,
    DateTime ExpiresAt,
    IReadOnlyList<string> Features);

/// <summary>
/// 16-Character Crockford Base32 + 48-bit HMAC-SHA256 Codec (ADR-019).
/// Encodes 80 bits into a formatted key (XXXX-XXXX-XXXX-XXXX) for air-gapped environments.
/// </summary>
public static class LicenseKeyCodec
{
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    private static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Default master key for development/mock. Can be injected or read from environment.
    public static readonly byte[] DefaultMasterKey = "VMTO-OFFLINE-LICENSE-MASTER-KEY"u8.ToArray();

    public static string Generate(
        LicensePlan plan,
        int maxConcurrentJobs,
        DateTime expiresAt,
        IReadOnlyList<string> features,
        byte[]? masterKey = null)
    {
        masterKey ??= DefaultMasterKey;
        var version = 1;
        var planVal = (int)plan & 0x07; // 3 bits
        var maxJobs = Math.Clamp(maxConcurrentJobs, 1, 32) - 1; // 5 bits (0..31 -> 1..32)

        var months = Math.Max(0, (expiresAt.Year - Epoch.Year) * 12 + (expiresAt.Month - Epoch.Month));
        months = Math.Min(months, 0x0FFF); // 12 bits (up to 4095 months)

        byte featureFlags = 0;
        if (features.Contains("vsphere", StringComparer.OrdinalIgnoreCase)) featureFlags |= 1 << 0;
        if (features.Contains("hyperv", StringComparer.OrdinalIgnoreCase)) featureFlags |= 1 << 1;
        if (features.Contains("cbt", StringComparer.OrdinalIgnoreCase) || features.Contains("incremental-sync", StringComparer.OrdinalIgnoreCase)) featureFlags |= 1 << 2;
        if (features.Contains("ops", StringComparer.OrdinalIgnoreCase) || features.Contains("api-access", StringComparer.OrdinalIgnoreCase)) featureFlags |= 1 << 3;

        // 32-bit (4 bytes) payload
        var payload = new byte[4];
        payload[0] = (byte)(((version & 0x0F) << 4) | (planVal & 0x07));
        payload[1] = (byte)(((maxJobs & 0x1F) << 3) | ((months >> 9) & 0x07));
        payload[2] = (byte)((months >> 1) & 0xFF);
        payload[3] = (byte)(((months & 0x01) << 7) | (featureFlags & 0x7F));

        // 48-bit (6 bytes) HMAC signature
        var signature = ComputeHmac48(payload, masterKey);

        var raw = new byte[10];
        Array.Copy(payload, 0, raw, 0, 4);
        Array.Copy(signature, 0, raw, 4, 6);

        return EncodeBase32(raw);
    }

    public static Result<DecodedLicenseKey> DecodeAndValidate(string formattedKey, byte[]? masterKey = null)
    {
        masterKey ??= DefaultMasterKey;

        var clean = formattedKey
            .Replace("-", "")
            .Replace("–", "") // en-dash
            .Replace("—", "") // em-dash
            .Replace("−", "") // minus sign
            .Replace(" ", "")
            .Trim()
            .ToUpperInvariant();

        if (clean.Length != 16)
        {
            return Result<DecodedLicenseKey>.Failure(ErrorCodes.License.Invalid, "授權碼格式錯誤，應為 16 碼英數組合。");
        }

        byte[] raw;
        try
        {
            raw = DecodeBase32(clean);
        }
        catch
        {
            return Result<DecodedLicenseKey>.Failure(ErrorCodes.License.Invalid, "授權碼包含無效字元。");
        }

        if (raw.Length != 10)
        {
            return Result<DecodedLicenseKey>.Failure(ErrorCodes.License.Invalid, "授權碼解碼長度不正確。");
        }

        var payload = raw[0..4];
        var actualSignature = raw[4..10];
        var expectedSignature = ComputeHmac48(payload, masterKey);

        if (!CryptographicOperations.FixedTimeEquals(actualSignature, expectedSignature))
        {
            return Result<DecodedLicenseKey>.Failure(ErrorCodes.License.Invalid, "授權碼無效或已被竄改。");
        }

        var version = (payload[0] >> 4) & 0x0F;
        var planVal = payload[0] & 0x07;
        var maxJobs = ((payload[1] >> 3) & 0x1F) + 1;

        var months = ((payload[1] & 0x07) << 9) | (payload[2] << 1) | ((payload[3] >> 7) & 0x01);
        var expiresAt = Epoch.AddMonths(months).AddMonths(1).AddDays(-1); // end of month

        var featureFlags = payload[3] & 0x7F;
        var features = new List<string>();
        if ((featureFlags & (1 << 0)) != 0) features.Add("vsphere");
        if ((featureFlags & (1 << 1)) != 0) features.Add("hyperv");
        if ((featureFlags & (1 << 2)) != 0) features.Add("incremental-sync");
        if ((featureFlags & (1 << 3)) != 0) features.Add("api-access");

        var plan = planVal switch
        {
            0 => LicensePlan.Trial,
            1 => LicensePlan.Standard,
            2 => LicensePlan.Enterprise,
            _ => LicensePlan.Standard
        };

        if (DateTime.UtcNow > expiresAt)
        {
            return Result<DecodedLicenseKey>.Failure(ErrorCodes.License.Expired, $"授權碼已於 {expiresAt:yyyy-MM-dd} 到期。");
        }

        return Result<DecodedLicenseKey>.Success(new DecodedLicenseKey(version, plan, maxJobs, expiresAt, features));
    }

    private static byte[] ComputeHmac48(byte[] payload, byte[] key)
    {
        using var hmac = new HMACSHA256(key);
        var full = hmac.ComputeHash(payload);
        var truncated = new byte[6];
        Array.Copy(full, 0, truncated, 0, 6);
        return truncated;
    }

    private static string EncodeBase32(byte[] data)
    {
        var sb = new StringBuilder(19);
        long buffer = 0;
        var bitsLeft = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                var index = (int)((buffer >> (bitsLeft - 5)) & 0x1F);
                bitsLeft -= 5;
                sb.Append(Alphabet[index]);
                if (sb.Length == 4 || sb.Length == 9 || sb.Length == 14)
                {
                    sb.Append('-');
                }
            }
        }

        if (bitsLeft > 0)
        {
            var index = (int)((buffer << (5 - bitsLeft)) & 0x1F);
            sb.Append(Alphabet[index]);
        }

        return sb.ToString();
    }

    private static byte[] DecodeBase32(string encoded)
    {
        var result = new List<byte>();
        long buffer = 0;
        var bitsLeft = 0;

        foreach (var c in encoded)
        {
            var val = Alphabet.IndexOf(c);
            if (val < 0)
            {
                throw new FormatException($"Invalid character: {c}");
            }

            buffer = (buffer << 5) | (byte)val;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                result.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                bitsLeft -= 8;
            }
        }

        return result.ToArray();
    }
}
