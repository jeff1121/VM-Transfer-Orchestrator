using VMTO.Application.Commands.Connections;
using VMTO.Application.DTOs;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.Connection;
using VMTO.Shared;

namespace VMTO.Application.Commands.Handlers;

public sealed class RunPreFlightCheckHandler : ICommandHandler<RunPreFlightCheckCommand, PreFlightCheckResultDto>
{
    private readonly IConnectionRepository _connectionRepository;
    private readonly IHyperVClient _hyperVClient;

    public RunPreFlightCheckHandler(
        IConnectionRepository connectionRepository,
        IHyperVClient hyperVClient)
    {
        _connectionRepository = connectionRepository;
        _hyperVClient = hyperVClient;
    }

    public async Task<Result<PreFlightCheckResultDto>> HandleAsync(RunPreFlightCheckCommand command, CancellationToken ct = default)
    {
        var connection = await _connectionRepository.GetByIdAsync(command.ConnectionId, ct);
        if (connection is null)
            return Result<PreFlightCheckResultDto>.Failure(ErrorCodes.Connection.NotFound, $"找不到連線 {command.ConnectionId}。");

        if (connection.Type != ConnectionType.HyperV)
            return Result<PreFlightCheckResultDto>.Failure(ErrorCodes.Connection.ValidationFailed, "Pre-flight 檢查目前支援 Hyper-V 連線。");

        return await _hyperVClient.RunPreFlightCheckAsync(command.ConnectionId, command.VmId, ct);
    }
}
