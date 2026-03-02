using VMTO.Application.Commands.Connections;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.Connection;
using VMTO.Shared;

namespace VMTO.Application.Commands.Handlers;

/// <summary>
/// 處理建立連線的命令。
/// 先加密密鑰，再建立 Connection 聚合並持久化。
/// </summary>
public sealed class CreateConnectionHandler : ICommandHandler<CreateConnectionCommand, Guid>
{
    private readonly IConnectionRepository _connectionRepository;
    private readonly IEncryptionService _encryptionService;

    public CreateConnectionHandler(IConnectionRepository connectionRepository, IEncryptionService encryptionService)
    {
        _connectionRepository = connectionRepository;
        _encryptionService = encryptionService;
    }

    public async Task<Result<Guid>> HandleAsync(CreateConnectionCommand command, CancellationToken ct = default)
    {
        // 加密密鑰
        var encryptedSecret = _encryptionService.Encrypt(command.Secret);

        var connection = new Connection(command.Name, command.Type, command.Endpoint, encryptedSecret);

        await _connectionRepository.AddAsync(connection, ct);
        return Result<Guid>.Success(connection.Id);
    }
}
