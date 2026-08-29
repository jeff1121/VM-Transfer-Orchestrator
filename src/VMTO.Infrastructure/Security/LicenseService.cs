using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.License;
using VMTO.Domain.Licensing;
using VMTO.Shared;

namespace VMTO.Infrastructure.Security;

public sealed class LicenseService : ILicenseService
{
    private readonly ILicenseRepository _licenseRepository;

    public LicenseService(ILicenseRepository licenseRepository)
    {
        _licenseRepository = licenseRepository;
    }

    public Task<Result<License>> ValidateAsync(string licenseKey, CancellationToken ct = default)
    {
        var decoded = LicenseKeyCodec.DecodeAndValidate(licenseKey);
        if (!decoded.IsSuccess)
        {
            return Task.FromResult(Result<License>.Failure(decoded.ErrorCode!, decoded.ErrorMessage!));
        }

        var d = decoded.Value!;
        var license = new License(
            licenseKey,
            d.Plan,
            d.Features,
            d.MaxConcurrentJobs,
            d.ExpiresAt,
            new Dictionary<string, string>(),
            "HMAC-SHA256-48");

        return Task.FromResult(Result<License>.Success(license));
    }

    public async Task<Result> ActivateAsync(string licenseKey, Dictionary<string, string> bindings, CancellationToken ct = default)
    {
        var decoded = LicenseKeyCodec.DecodeAndValidate(licenseKey);
        if (!decoded.IsSuccess)
        {
            return Result.Failure(decoded.ErrorCode!, decoded.ErrorMessage!);
        }

        var d = decoded.Value!;

        var existing = await _licenseRepository.GetByKeyAsync(licenseKey, ct);
        if (existing is not null)
        {
            return Result.Success();
        }

        var license = new License(
            licenseKey,
            d.Plan,
            d.Features,
            d.MaxConcurrentJobs,
            d.ExpiresAt,
            bindings,
            "HMAC-SHA256-48");

        await _licenseRepository.AddAsync(license, ct);
        return Result.Success();
    }

    public async Task<Result<bool>> CheckFeatureAsync(string feature, CancellationToken ct = default)
    {
        var license = await _licenseRepository.GetActiveAsync(ct);
        if (license is null || !license.IsValid())
        {
            return Result<bool>.Success(true); // default open in dev/unlicensed
        }

        return Result<bool>.Success(license.HasFeature(feature));
    }

    public async Task<Result<int>> GetConcurrentJobLimitAsync(CancellationToken ct = default)
    {
        var license = await _licenseRepository.GetActiveAsync(ct);
        if (license is null || !license.IsValid())
        {
            return Result<int>.Success(10);
        }

        return Result<int>.Success(license.MaxConcurrentJobs);
    }
}
