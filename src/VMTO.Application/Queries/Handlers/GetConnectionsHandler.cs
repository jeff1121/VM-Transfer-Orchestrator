using VMTO.Application.DTOs;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Queries.Connections;
using VMTO.Shared;

namespace VMTO.Application.Queries.Handlers;

/// <summary>
/// 處理列出連線的查詢。
/// 將結果映射為 ConnectionDto 清單，不包含密鑰資訊。
/// </summary>
public sealed class GetConnectionsHandler : IQueryHandler<GetConnectionsQuery, IReadOnlyList<ConnectionDto>>
{
    private readonly IConnectionRepository _connectionRepository;

    public GetConnectionsHandler(IConnectionRepository connectionRepository)
    {
        _connectionRepository = connectionRepository;
    }

    public async Task<Result<IReadOnlyList<ConnectionDto>>> HandleAsync(GetConnectionsQuery query, CancellationToken ct = default)
    {
        var connections = await _connectionRepository.ListAsync(query.Page, query.PageSize, ct);

        // 映射為 DTO 時不包含密鑰
        var dtos = connections.Select(c => new ConnectionDto(
            c.Id, c.Name, c.Type, c.Endpoint, c.ValidatedAt, c.CreatedAt)).ToList();

        return Result<IReadOnlyList<ConnectionDto>>.Success(dtos);
    }
}
