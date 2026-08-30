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
    private readonly ISourcePlatformProviderFactory _sourceFactory;
    private readonly ITargetPlatformProviderFactory _targetFactory;

    public ValidateConnectionHandler(
        IConnectionRepository connectionRepository,
        ISourcePlatformProviderFactory sourceFactory,
        ITargetPlatformProviderFactory targetFactory)
    {
        _connectionRepository = connectionRepository;
        _sourceFactory = sourceFactory;
        _targetFactory = targetFactory;
    }

    public async Task<Result> HandleAsync(ValidateConnectionCommand command, CancellationToken ct = default)
    {
        var connection = await _connectionRepository.GetByIdAsync(command.ConnectionId, ct);
        if (connection is null)
            return Result.Failure(ErrorCodes.Connection.NotFound, $"找不到連線 {command.ConnectionId}。");

        Result validationResult;
        try
        {
            if (connection.Type == PlatformKind.ProxmoxVE)
            {
                var targetProvider = _targetFactory.GetProvider(connection.Type);
                var createResult = await targetProvider.CreateVmAsync(connection.Id, "__vmto_conn_test", 1, 512, ct);
                validationResult = createResult.IsSuccess ? Result.Success() : Result.Failure(createResult.ErrorCode!, createResult.ErrorMessage!);
            }
            else
            {
                var sourceProvider = _sourceFactory.GetProvider(connection.Type);
                var listResult = await sourceProvider.ListVmsAsync(connection.Id, ct);
                validationResult = listResult.IsSuccess ? Result.Success() : Result.Failure(listResult.ErrorCode!, listResult.ErrorMessage!);
            }
        }
        catch (NotSupportedException ex)
        {
            validationResult = Result.Failure(ErrorCodes.Connection.ValidationFailed, ex.Message);
        }

        if (!validationResult.IsSuccess)
            return Result.Failure(ErrorCodes.Connection.ValidationFailed, validationResult.ErrorMessage ?? "連線驗證失敗。");

        connection.MarkValidated();
        await _connectionRepository.UpdateAsync(connection, ct);
        return Result.Success();
    }
}
