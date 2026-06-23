using FluentAssertions;
using NSubstitute;
using VMTO.Application.Commands.Connections;
using VMTO.Application.Commands.Handlers;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.Connection;
using VMTO.Domain.ValueObjects;
using VMTO.Shared;

namespace VMTO.Application.Tests;

/// <summary>
/// ValidateConnectionHandler 的單元測試。
/// 驗證連線驗證的各種情境。
/// </summary>
public sealed class ValidateConnectionHandlerTests
{
    private readonly IConnectionRepository _connectionRepository = Substitute.For<IConnectionRepository>();
    private readonly IVSphereClient _vSphereClient = Substitute.For<IVSphereClient>();
    private readonly IPveClient _pveClient = Substitute.For<IPveClient>();
    private readonly ValidateConnectionHandler _handler;

    public ValidateConnectionHandlerTests()
    {
        _handler = new ValidateConnectionHandler(_connectionRepository, _vSphereClient, _pveClient);
    }

    [Fact]
    public async Task HandleAsync_找不到Connection回傳錯誤()
    {
        var connId = Guid.NewGuid();
        _connectionRepository.GetByIdAsync(connId, Arg.Any<CancellationToken>()).Returns((Connection?)null);

        var result = await _handler.HandleAsync(new ValidateConnectionCommand(connId));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Connection.NotFound);
    }

    [Fact]
    public async Task HandleAsync_驗證成功時標記已驗證()
    {
        var connection = new Connection("vcenter-prod", ConnectionType.VSphere, "https://vcenter.local", new EncryptedSecret("cipher", "key-1"));
        _connectionRepository.GetByIdAsync(connection.Id, Arg.Any<CancellationToken>()).Returns(connection);
        _vSphereClient.ListVmsAsync(connection.Id, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DTOs.VmInfoDto>>.Success(Array.Empty<DTOs.VmInfoDto>()));

        var result = await _handler.HandleAsync(new ValidateConnectionCommand(connection.Id));

        result.IsSuccess.Should().BeTrue();
        connection.ValidatedAt.Should().NotBeNull();
        await _connectionRepository.Received(1).UpdateAsync(connection, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_驗證失敗時回傳錯誤()
    {
        var connection = new Connection("vcenter-prod", ConnectionType.VSphere, "https://vcenter.local", new EncryptedSecret("cipher", "key-1"));
        _connectionRepository.GetByIdAsync(connection.Id, Arg.Any<CancellationToken>()).Returns(connection);
        _vSphereClient.ListVmsAsync(connection.Id, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DTOs.VmInfoDto>>.Failure(ErrorCodes.General.InternalError, "連線失敗"));

        var result = await _handler.HandleAsync(new ValidateConnectionCommand(connection.Id));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Connection.ValidationFailed);
        connection.ValidatedAt.Should().BeNull();
    }
}
