using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;

namespace VMTO.API.Endpoints;

public static class LicenseEndpoints
{
    public static void MapLicenseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/license").WithTags("License");

        group.MapGet("/", GetLicense);
        group.MapPost("/activate", ActivateLicense);
    }

    private static async Task<IResult> GetLicense(ILicenseRepository repo, CancellationToken ct)
    {
        var license = await repo.GetActiveAsync(ct);
        if (license is null) return Results.NotFound();
        return Results.Ok(new
        {
            license.Id,
            license.Plan,
            license.Features,
            license.MaxConcurrentJobs,
            license.ExpiresAt,
            IsValid = license.IsValid(),
            license.CreatedAt
        });
    }

    private static async Task<IResult> ActivateLicense(
        ActivateLicenseRequest request,
        ILicenseService licenseService,
        CancellationToken ct)
    {
        var result = await licenseService.ActivateAsync(request.LicenseKey, request.Bindings ?? [], ct);
        if (!result.IsSuccess)
        {
            return Results.Problem(
                title: "License activation failed",
                detail: result.ErrorMessage,
                statusCode: StatusCodes.Status400BadRequest);
        }
        return Results.Ok(new { activated = true });
    }
}

public sealed record ActivateLicenseRequest(
    string LicenseKey,
    Dictionary<string, string>? Bindings);
