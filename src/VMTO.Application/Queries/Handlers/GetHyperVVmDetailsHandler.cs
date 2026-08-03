using VMTO.Application.DTOs;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Application.Queries.Connections;
using VMTO.Domain.Aggregates.Connection;
using VMTO.Shared;

namespace VMTO.Application.Queries.Handlers;

public sealed class GetHyperVVmDetailsHandler : IQueryHandler<GetHyperVVmDetailsQuery, HyperVVmDetailsDto>
{
    private readonly IConnectionRepository _connectionRepository;
    private readonly IHyperVClient _hyperVClient;

    public GetHyperVVmDetailsHandler(
        IConnectionRepository connectionRepository,
        IHyperVClient hyperVClient)
    {
        _connectionRepository = connectionRepository;
        _hyperVClient = hyperVClient;
    }

    public async Task<Result<HyperVVmDetailsDto>> HandleAsync(GetHyperVVmDetailsQuery query, CancellationToken ct = default)
    {
        var connection = await _connectionRepository.GetByIdAsync(query.ConnectionId, ct);
        if (connection is null)
            return Result<HyperVVmDetailsDto>.Failure(ErrorCodes.Connection.NotFound, $"找不到連線 {query.ConnectionId}。");

        if (connection.Type != ConnectionType.HyperV)
            return Result<HyperVVmDetailsDto>.Failure(ErrorCodes.Connection.ValidationFailed, "僅支援 Hyper-V 連線之詳細資訊查詢。");

        return await _hyperVClient.GetVmDetailsAsync(query.ConnectionId, query.VmId, ct);
    }
}

public sealed class ListVmsHandler : IQueryHandler<ListVmsQuery, IReadOnlyList<VmInfoDto>>
{
    private readonly IConnectionRepository _connectionRepository;
    private readonly IVSphereClient _vSphereClient;
    private readonly IHyperVClient _hyperVClient;

    public ListVmsHandler(
        IConnectionRepository connectionRepository,
        IVSphereClient vSphereClient,
        IHyperVClient hyperVClient)
    {
        _connectionRepository = connectionRepository;
        _vSphereClient = vSphereClient;
        _hyperVClient = hyperVClient;
    }

    public async Task<Result<IReadOnlyList<VmInfoDto>>> HandleAsync(ListVmsQuery query, CancellationToken ct = default)
    {
        var connection = await _connectionRepository.GetByIdAsync(query.ConnectionId, ct);
        if (connection is null)
            return Result<IReadOnlyList<VmInfoDto>>.Failure(ErrorCodes.Connection.NotFound, $"找不到連線 {query.ConnectionId}。");

        return connection.Type switch
        {
            ConnectionType.VSphere => await _vSphereClient.ListVmsAsync(query.ConnectionId, ct),
            ConnectionType.HyperV => await _hyperVClient.ListVmsAsync(query.ConnectionId, ct),
            _ => Result<IReadOnlyList<VmInfoDto>>.Failure(ErrorCodes.Connection.ValidationFailed, "不支援的來源平台連線類型。")
        };
    }
}
