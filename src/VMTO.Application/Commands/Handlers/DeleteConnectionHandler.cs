using VMTO.Application.Commands.Connections;
using VMTO.Application.Ports.Repositories;
using VMTO.Shared;

namespace VMTO.Application.Commands.Handlers;

/// <summary>
/// 處理刪除連線的命令。
/// 載入連線確認存在後刪除。
/// </summary>
public sealed class DeleteConnectionHandler : ICommandHandler<DeleteConnectionCommand>
{
    private readonly IConnectionRepository _connectionRepository;

    public DeleteConnectionHandler(IConnectionRepository connectionRepository)
    {
        _connectionRepository = connectionRepository;
    }

    public async Task<Result> HandleAsync(DeleteConnectionCommand command, CancellationToken ct = default)
    {
        var connection = await _connectionRepository.GetByIdAsync(command.ConnectionId, ct);
        if (connection is null)
            return Result.Failure(ErrorCodes.Connection.NotFound, $"找不到連線 {command.ConnectionId}。");

        await _connectionRepository.DeleteAsync(command.ConnectionId, ct);
        return Result.Success();
    }
}
