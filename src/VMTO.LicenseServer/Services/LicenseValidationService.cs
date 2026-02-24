using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VMTO.Domain.Aggregates.License;

namespace VMTO.LicenseServer.Services;

public sealed class LicenseValidationService
{
    private readonly IConfiguration _configuration;

    public LicenseValidationService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public LicenseValidationResult Validate(License license)
    {
        if (license.IsExpired())
            return LicenseValidationResult.Fail("LICENSE_EXPIRED", "The license has expired.");

        if (!VerifySignature(license))
            return LicenseValidationResult.Fail("INVALID_SIGNATURE", "The license signature is invalid.");

        return LicenseValidationResult.Ok(license);
    }

    public bool HasFeature(License license, string feature) => license.HasFeature(feature);

    public int GetConcurrentJobLimit(License license) => license.MaxConcurrentJobs;

    public bool MatchesBindings(License license, IDictionary<string, string> bindings)
    {
        if (license.ActivationBindings.Count == 0)
            return true;

        foreach (var (key, value) in license.ActivationBindings)
        {
            if (!bindings.TryGetValue(key, out var provided)
                || !string.Equals(provided, value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private bool VerifySignature(License license)
    {
        var signingKey = GetSigningKey();
        var payload = JsonSerializer.Serialize(new
        {
            key = license.Key,
            plan = license.Plan,
            features = license.Features,
            maxConcurrentJobs = license.MaxConcurrentJobs,
            expiresAt = license.ExpiresAt
        });
        using var hmac = new HMACSHA256(signingKey);
        var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var actual = Convert.FromBase64String(license.Signature);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private byte[] GetSigningKey()
    {
        var keyString = _configuration["License:SigningKey"]
            ?? throw new InvalidOperationException(
                "License signing key is not configured. Set 'License:SigningKey' in configuration or the LICENSE__SIGNINGKEY environment variable.");

        return Convert.FromBase64String(keyString);
    }
}

public sealed record LicenseValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public License? License { get; init; }

    public static LicenseValidationResult Ok(License license) =>
        new() { IsValid = true, License = license };

    public static LicenseValidationResult Fail(string code, string message) =>
        new() { IsValid = false, ErrorCode = code, ErrorMessage = message };
}
