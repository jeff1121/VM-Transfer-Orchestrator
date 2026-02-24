using System.Text.Json.Serialization;
using VMTO.Application.Ports.Repositories;
using VMTO.Domain.Aggregates.License;
using VMTO.LicenseServer.Services;

namespace VMTO.LicenseServer.Endpoints;

public static class LicenseEndpoints
{
    public static void MapLicenseServerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/licenses").WithTags("Licenses");

        group.MapPost("/generate", GenerateAsync).WithName("GenerateLicense");
        group.MapPost("/validate", ValidateAsync).WithName("ValidateLicense");
        group.MapPost("/activate", ActivateAsync).WithName("ActivateLicense");
        group.MapGet("/info/{key}", GetInfoAsync).WithName("GetLicenseInfo");
    }

    private static async Task<IResult> GenerateAsync(
        GenerateLicenseRequest request,
        LicenseGenerationService generationService,
        ILicenseRepository repository,
        CancellationToken ct)
    {
        if (!Enum.TryParse<LicensePlan>(request.Plan, ignoreCase: true, out var plan))
            return Results.BadRequest(new { error = $"Invalid plan: {request.Plan}" });

        var license = await generationService.GenerateAsync(
            plan,
            request.Features ?? [],
            request.MaxConcurrentJobs,
            request.ExpiresAt,
            request.ActivationBindings ?? new Dictionary<string, string>(),
            repository,
            ct);

        return Results.Ok(new
        {
            license.Key,
            Plan = license.Plan.ToString(),
            license.Features,
            license.MaxConcurrentJobs,
            license.ExpiresAt,
            license.Signature
        });
    }

    private static async Task<IResult> ValidateAsync(
        ValidateLicenseRequest request,
        LicenseValidationService validationService,
        ILicenseRepository repository,
        CancellationToken ct)
    {
        var license = await repository.GetByKeyAsync(request.Key, ct);
        if (license is null)
            return Results.BadRequest(new { error = "License key not found." });

        var result = validationService.Validate(license);
        if (!result.IsValid)
            return Results.BadRequest(new { result.ErrorCode, result.ErrorMessage });

        return Results.Ok(new
        {
            license.Key,
            Plan = license.Plan.ToString(),
            license.Features,
            license.MaxConcurrentJobs,
            license.ExpiresAt
        });
    }

    private static async Task<IResult> ActivateAsync(
        ActivateLicenseRequest request,
        LicenseValidationService validationService,
        ILicenseRepository repository,
        CancellationToken ct)
    {
        var license = await repository.GetByKeyAsync(request.Key, ct);
        if (license is null)
            return Results.BadRequest(new { error = "License key not found." });

        var result = validationService.Validate(license);
        if (!result.IsValid)
            return Results.BadRequest(new { result.ErrorCode, result.ErrorMessage });

        if (!validationService.MatchesBindings(license, request.Bindings ?? new Dictionary<string, string>()))
            return Results.BadRequest(new { error = "Activation bindings do not match." });

        return Results.Ok(new { status = "activated", license.Key });
    }

    private static async Task<IResult> GetInfoAsync(
        string key,
        ILicenseRepository repository,
        CancellationToken ct)
    {
        var license = await repository.GetByKeyAsync(key, ct);
        if (license is null)
            return Results.NotFound(new { error = "License key not found." });

        return Results.Ok(new
        {
            license.Key,
            Plan = license.Plan.ToString(),
            license.Features,
            license.MaxConcurrentJobs,
            license.ExpiresAt,
            IsValid = license.IsValid(),
            license.CreatedAt
        });
    }
}

public sealed record GenerateLicenseRequest(
    string Plan,
    List<string>? Features,
    int MaxConcurrentJobs,
    DateTime ExpiresAt,
    Dictionary<string, string>? ActivationBindings);

public sealed record ValidateLicenseRequest(string Key);

public sealed record ActivateLicenseRequest(
    string Key,
    Dictionary<string, string>? Bindings);
