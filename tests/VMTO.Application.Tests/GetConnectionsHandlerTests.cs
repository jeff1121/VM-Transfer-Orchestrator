using FluentAssertions;
using NSubstitute;
using VMTO.Application.DTOs;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Queries.Connections;
using VMTO.Application.Queries.Handlers;
using VMTO.Domain.Aggregates.Connection;
using VMTO.Domain.ValueObjects;

namespace VMTO.Application.Tests;

/// <summary>
/// GetConnectionsHandler 的單元測試。
/// 驗證列出連線清單的邏輯。
/// </summary>
public sealed class GetConnectionsHandlerTests
{
    private readonly IConnectionRepository _connectionRepository = Substitute.For<IConnectionRepository>();
    private readonly GetConnectionsHandler _handler;

    public GetConnectionsHandlerTests()
    {
        _handler = new GetConnectionsHandler(_connectionRepository);
    }

    [Fact]
    public async Task HandleAsync_成功列出Connections()
    {
        var conn1 = new Connection("vcenter-prod", ConnectionType.VSphere, "https://vcenter.local", new EncryptedSecret("cipher1", "key-1"));
        var conn2 = new Connection("pve-node", ConnectionType.ProxmoxVE, "https://pve.local:8006", new EncryptedSecret("cipher2", "key-2"));
        _connectionRepository.ListAsync(1, 10, Arg.Any<CancellationToken>())
            .Returns(new List<Connection> { conn1, conn2 });

        var result = await _handler.HandleAsync(new GetConnectionsQuery(1, 10));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value![0].Name.Should().Be("vcenter-prod");
        result.Value[1].Name.Should().Be("pve-node");

        // DTO 不應包含密鑰資訊（ConnectionDto 不含 EncryptedSecret 欄位）
        result.Value[0].Should().BeOfType<ConnectionDto>();
    }
}
