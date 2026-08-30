using VMTO.Domain.Aggregates.License;
using VMTO.Domain.Licensing;

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
        var decoded = LicenseKeyCodec.DecodeAndValidate(license.Key, GetMasterKey());
        if (!decoded.IsSuccess)
        {
            return LicenseValidationResult.Fail(decoded.ErrorCode!, decoded.ErrorMessage!);
        }

        return LicenseValidationResult.Ok(license);
    }

    public bool HasFeature(License license, string feature) => license.HasFeature(feature);

    public int GetConcurrentJobLimit(License license) => license.MaxConcurrentJobs;

    public bool MatchesBindings(License license, IDictionary<string, string> bindings) => true;

    private byte[]? GetMasterKey()
    {
        var keyString = _configuration["License:SigningKey"];
        if (string.IsNullOrWhiteSpace(keyString)) return null;
        try { return Convert.FromBase64String(keyString); }
        catch { return null; }
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
