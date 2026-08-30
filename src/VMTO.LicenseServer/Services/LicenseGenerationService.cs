using VMTO.Application.Ports.Repositories;
using VMTO.Domain.Aggregates.License;
using VMTO.Domain.Licensing;

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
        var key = LicenseKeyCodec.Generate(plan, maxConcurrentJobs, expiresAt, features.ToList(), GetMasterKey());
        var signature = "HMAC-SHA256-48";

        var license = new License(key, plan, features, maxConcurrentJobs, expiresAt, activationBindings, signature);
        await repository.AddAsync(license, ct);
        return license;
    }

    public string GenerateKeyOnly(LicensePlan plan, IEnumerable<string> features, int maxConcurrentJobs, DateTime expiresAt)
    {
        return LicenseKeyCodec.Generate(plan, maxConcurrentJobs, expiresAt, features.ToList(), GetMasterKey());
    }

    private byte[]? GetMasterKey()
    {
        var keyString = _configuration["License:SigningKey"];
        if (string.IsNullOrWhiteSpace(keyString)) return null;
        try { return Convert.FromBase64String(keyString); }
        catch { return null; }
    }
}
