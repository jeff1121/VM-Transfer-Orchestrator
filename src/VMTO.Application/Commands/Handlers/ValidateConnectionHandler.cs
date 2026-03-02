using VMTO.Application.Commands.Connections;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.Connection;
using VMTO.Shared;

namespace VMTO.Application.Commands.Handlers;

/// <summary>
/// 處理驗證連線的命令。
/// 根據連線類型使用對應的用戶端驗證連線是否可用，成功時標記已驗證。
/// </summary>
public sealed class ValidateConnectionHandler : ICommandHandler<ValidateConnectionCommand>
{
    private readonly IConnectionRepository _connectionRepository;
    private readonly IVSphereClient _vSphereClient;
    private readonly IPveClient _pveClient;

    public ValidateConnectionHandler(
        IConnectionRepository connectionRepository,
        IVSphereClient vSphereClient,
        IPveClient pveClient)
    {
        _connectionRepository = connectionRepository;
        _vSphereClient = vSphereClient;
        _pveClient = pveClient;
    }

    public async Task<Result> HandleAsync(ValidateConnectionCommand command, CancellationToken ct = default)
    {
        var connection = await _connectionRepository.GetByIdAsync(command.ConnectionId, ct);
        if (connection is null)
            return Result.Failure(ErrorCodes.Connection.NotFound, $"找不到連線 {command.ConnectionId}。");

        // 根據連線類型使用對應用戶端進行驗證
        Result validationResult = connection.Type switch
        {
            ConnectionType.VSphere => await ValidateVSphereAsync(connection.Id, ct),
            ConnectionType.ProxmoxVE => await ValidatePveAsync(connection.Id, ct),
            _ => Result.Failure(ErrorCodes.Connection.ValidationFailed, "不支援的連線類型。")
        };

        if (!validationResult.IsSuccess)
            return Result.Failure(ErrorCodes.Connection.ValidationFailed, validationResult.ErrorMessage ?? "連線驗證失敗。");

        connection.MarkValidated();
        await _connectionRepository.UpdateAsync(connection, ct);
        return Result.Success();
    }

    private async Task<Result> ValidateVSphereAsync(Guid connectionId, CancellationToken ct)
    {
        var result = await _vSphereClient.ListVmsAsync(connectionId, ct);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.ErrorCode!, result.ErrorMessage!);
    }

    private async Task<Result> ValidatePveAsync(Guid connectionId, CancellationToken ct)
    {
        // 嘗試建立一個測試用 VM 來驗證連線（使用最小配置）
        var result = await _pveClient.CreateVmAsync(connectionId, "__vmto_conn_test", 1, 512, ct);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.ErrorCode!, result.ErrorMessage!);
    }
}
