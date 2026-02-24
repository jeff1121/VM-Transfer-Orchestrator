using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VMTO.Application.Ports.Repositories;
using VMTO.Domain.Aggregates.License;

namespace VMTO.LicenseServer.Services;

public sealed class LicenseGenerationService
{
    private readonly IConfiguration _configuration;

    public LicenseGenerationService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<License> GenerateAsync(
        LicensePlan plan,
        IEnumerable<string> features,
        int maxConcurrentJobs,
        DateTime expiresAt,
        IDictionary<string, string> activationBindings,
        ILicenseRepository repository,
        CancellationToken ct = default)
    {
        var key = GenerateKey();
        var signature = Sign(key, plan, features, maxConcurrentJobs, expiresAt);

        var license = new License(key, plan, features, maxConcurrentJobs, expiresAt, activationBindings, signature);
        await repository.AddAsync(license, ct);
        return license;
    }

    internal static string GenerateKey()
    {
        Span<byte> buffer = stackalloc byte[16];
        RandomNumberGenerator.Fill(buffer);
        var hex = Convert.ToHexString(buffer);
        return $"VMTO-{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}";
    }

    internal string Sign(string key, LicensePlan plan, IEnumerable<string> features, int maxConcurrentJobs, DateTime expiresAt)
    {
        var signingKey = GetSigningKey();
        var payload = JsonSerializer.Serialize(new { key, plan, features, maxConcurrentJobs, expiresAt });
        using var hmac = new HMACSHA256(signingKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }

    private byte[] GetSigningKey()
    {
        var keyString = _configuration["License:SigningKey"]
            ?? throw new InvalidOperationException(
                "License signing key is not configured. Set 'License:SigningKey' in configuration or the LICENSE__SIGNINGKEY environment variable.");

        return Convert.FromBase64String(keyString);
    }
}
