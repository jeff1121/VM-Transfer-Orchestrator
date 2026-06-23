using FluentAssertions;
using NSubstitute;
using VMTO.Application.Commands.Connections;
using VMTO.Application.Commands.Handlers;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.Connection;
using VMTO.Domain.ValueObjects;

namespace VMTO.Application.Tests;

/// <summary>
/// CreateConnectionHandler 的單元測試。
/// 驗證連線建立及密鑰加密邏輯。
/// </summary>
public sealed class CreateConnectionHandlerTests
{
    private readonly IConnectionRepository _connectionRepository = Substitute.For<IConnectionRepository>();
    private readonly IEncryptionService _encryptionService = Substitute.For<IEncryptionService>();
    private readonly CreateConnectionHandler _handler;

    public CreateConnectionHandlerTests()
    {
        _handler = new CreateConnectionHandler(_connectionRepository, _encryptionService);
    }

    [Fact]
    public async Task HandleAsync_成功建立Connection()
    {
        _encryptionService.Encrypt("my-secret").Returns(new EncryptedSecret("encrypted-value", "key-1"));

        var command = new CreateConnectionCommand("vcenter-prod", ConnectionType.VSphere, "https://vcenter.local", "my-secret");

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        await _connectionRepository.Received(1).AddAsync(Arg.Any<Connection>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_正確加密密鑰()
    {
        var expectedSecret = new EncryptedSecret("encrypted-data", "key-id");
        _encryptionService.Encrypt("plain-secret").Returns(expectedSecret);

        Connection? savedConnection = null;
        await _connectionRepository.AddAsync(Arg.Do<Connection>(c => savedConnection = c), Arg.Any<CancellationToken>());

        var command = new CreateConnectionCommand("pve-node", ConnectionType.ProxmoxVE, "https://pve.local:8006", "plain-secret");

        await _handler.HandleAsync(command);

        _encryptionService.Received(1).Encrypt("plain-secret");
        savedConnection.Should().NotBeNull();
        savedConnection!.EncryptedSecret.Should().Be(expectedSecret);
    }
}
