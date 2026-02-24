using VMTO.Application.DTOs;
using VMTO.Application.Ports.Repositories;
using VMTO.Domain.Aggregates.Artifact;

namespace VMTO.API.Endpoints;

public static class ArtifactEndpoints
{
    public static void MapArtifactEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/artifacts").WithTags("Artifacts");

        group.MapGet("/", ListArtifacts);
    }

    private static async Task<IResult> ListArtifacts(
        IArtifactRepository repo,
        Guid jobId,
        CancellationToken ct)
    {
        var artifacts = await repo.ListByJobIdAsync(jobId, ct);
        return Results.Ok(artifacts.Select(MapToDto));
    }

    private static ArtifactDto MapToDto(Artifact a) =>
        new(a.Id,
            a.FileName,
            a.Format,
            a.Checksum.Algorithm,
            a.Checksum.Value,
            a.SizeBytes,
            a.StorageUri,
            a.CreatedAt);
}
