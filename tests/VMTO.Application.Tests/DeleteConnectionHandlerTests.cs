using FluentAssertions;
using NSubstitute;
using VMTO.Application.Commands.Connections;
using VMTO.Application.Commands.Handlers;
using VMTO.Application.Ports.Repositories;
using VMTO.Domain.Aggregates.Connection;
using VMTO.Domain.ValueObjects;
using VMTO.Shared;

namespace VMTO.Application.Tests;

/// <summary>
/// DeleteConnectionHandler 的單元測試。
/// 驗證刪除連線的各種情境。
/// </summary>
public sealed class DeleteConnectionHandlerTests
{
    private readonly IConnectionRepository _connectionRepository = Substitute.For<IConnectionRepository>();
    private readonly DeleteConnectionHandler _handler;

    public DeleteConnectionHandlerTests()
    {
        _handler = new DeleteConnectionHandler(_connectionRepository);
    }

    [Fact]
    public async Task HandleAsync_找不到Connection回傳錯誤()
    {
        var connId = Guid.NewGuid();
        _connectionRepository.GetByIdAsync(connId, Arg.Any<CancellationToken>()).Returns((Connection?)null);

        var result = await _handler.HandleAsync(new DeleteConnectionCommand(connId));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Connection.NotFound);
    }

    [Fact]
    public async Task HandleAsync_成功刪除Connection()
    {
        var connection = new Connection("vcenter-prod", ConnectionType.VSphere, "https://vcenter.local", new EncryptedSecret("cipher", "key-1"));
        _connectionRepository.GetByIdAsync(connection.Id, Arg.Any<CancellationToken>()).Returns(connection);

        var result = await _handler.HandleAsync(new DeleteConnectionCommand(connection.Id));

        result.IsSuccess.Should().BeTrue();
        await _connectionRepository.Received(1).DeleteAsync(connection.Id, Arg.Any<CancellationToken>());
    }
}
