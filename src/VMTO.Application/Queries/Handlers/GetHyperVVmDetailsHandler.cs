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

        if (connection.Type != PlatformKind.HyperV)
            return Result<HyperVVmDetailsDto>.Failure(ErrorCodes.Connection.ValidationFailed, "僅支援 Hyper-V 連線之詳細資訊查詢。");

        var inspection = await _hyperVClient.InspectAsync(query.ConnectionId, query.VmId, ct);
        if (!inspection.IsSuccess)
            return Result<HyperVVmDetailsDto>.Failure(inspection.ErrorCode!, inspection.ErrorMessage!);

        var value = inspection.Value!;
        return Result<HyperVVmDetailsDto>.Success(new HyperVVmDetailsDto(
            value.Id,
            value.Name,
            value.State,
            value.CpuCount,
            value.MemoryBytes,
            value.GuestOs ?? string.Empty,
            value.CheckpointCount,
            value.Disks.Select(d => new HyperVVmDiskInfoDto(d.DiskKey, d.Path, d.SizeBytes, d.Format)).ToList()));
    }
}

public sealed class ListVmsHandler : IQueryHandler<ListVmsQuery, IReadOnlyList<VmInfoDto>>
{
    private readonly IConnectionRepository _connectionRepository;
    private readonly ISourcePlatformProviderFactory _sourceFactory;

    public ListVmsHandler(
        IConnectionRepository connectionRepository,
        ISourcePlatformProviderFactory sourceFactory)
    {
        _connectionRepository = connectionRepository;
        _sourceFactory = sourceFactory;
    }

    public async Task<Result<IReadOnlyList<VmInfoDto>>> HandleAsync(ListVmsQuery query, CancellationToken ct = default)
    {
        var connection = await _connectionRepository.GetByIdAsync(query.ConnectionId, ct);
        if (connection is null)
            return Result<IReadOnlyList<VmInfoDto>>.Failure(ErrorCodes.Connection.NotFound, $"找不到連線 {query.ConnectionId}。");

        try
        {
            var provider = _sourceFactory.GetProvider(connection.Type);
            return await provider.ListVmsAsync(query.ConnectionId, ct);
        }
        catch (NotSupportedException ex)
        {
            return Result<IReadOnlyList<VmInfoDto>>.Failure(ErrorCodes.Connection.ValidationFailed, ex.Message);
        }
    }
}
