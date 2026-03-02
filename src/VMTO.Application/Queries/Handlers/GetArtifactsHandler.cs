using VMTO.Application.DTOs;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Queries.Artifacts;
using VMTO.Shared;

namespace VMTO.Application.Queries.Handlers;

/// <summary>
/// 處理列出成品的查詢。
/// 根據 JobId 列出所有成品並映射為 ArtifactDto 清單。
/// </summary>
public sealed class GetArtifactsHandler : IQueryHandler<GetArtifactsQuery, IReadOnlyList<ArtifactDto>>
{
    private readonly IArtifactRepository _artifactRepository;

    public GetArtifactsHandler(IArtifactRepository artifactRepository)
    {
        _artifactRepository = artifactRepository;
    }

    public async Task<Result<IReadOnlyList<ArtifactDto>>> HandleAsync(GetArtifactsQuery query, CancellationToken ct = default)
    {
        var artifacts = await _artifactRepository.ListByJobIdAsync(query.JobId, ct);

        var dtos = artifacts.Select(a => new ArtifactDto(
            a.Id, a.FileName, a.Format,
            a.Checksum.Algorithm, a.Checksum.Value,
            a.SizeBytes, a.StorageUri, a.CreatedAt)).ToList();

        return Result<IReadOnlyList<ArtifactDto>>.Success(dtos);
    }
}
