using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VMTO.Domain.Aggregates.Connection;
using VMTO.Domain.ValueObjects;
using VMTO.Infrastructure.Persistence;
using VMTO.Infrastructure.Persistence.Repositories;
using VMTO.IntegrationTests.Fixtures;

namespace VMTO.IntegrationTests.Repositories;

/// <summary>
/// ConnectionRepository 整合測試，使用真實 PostgreSQL 容器驗證儲存庫操作。
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class ConnectionRepositoryTests(PostgreSqlFixture fixture)
{
    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options);

    [Fact]
    public async Task AddAsync_應成功新增_Connection()
    {
        // Arrange
        await using var context = CreateContext();
        var repo = new ConnectionRepository(context);
        var conn = new Connection("test-vsphere", ConnectionType.VSphere, "https://vcenter.local", new EncryptedSecret("encrypted", "key-1"));

        // Act
        await repo.AddAsync(conn);

        // Assert — 確認已寫入資料庫
        var saved = await context.Connections.FindAsync(conn.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("test-vsphere");
    }

    [Fact]
    public async Task DeleteAsync_應刪除_Connection()
    {
        // Arrange
        await using var context = CreateContext();
        var repo = new ConnectionRepository(context);
        var conn = new Connection("to-delete", ConnectionType.ProxmoxVE, "https://pve.local", new EncryptedSecret("encrypted"));
        await repo.AddAsync(conn);

        // Act
        await repo.DeleteAsync(conn.Id);

        // Assert — 確認已從資料庫刪除
        var deleted = await context.Connections.FindAsync(conn.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_應持久化驗證時間()
    {
        // Arrange
        await using var context = CreateContext();
        var repo = new ConnectionRepository(context);
        var conn = new Connection("to-validate", ConnectionType.VSphere, "https://vcenter.local", new EncryptedSecret("encrypted"));
        await repo.AddAsync(conn);

        // Act — 標記為已驗證
        conn.MarkValidated();
        await repo.UpdateAsync(conn);

        // Assert — 驗證時間已持久化
        await using var readContext = CreateContext();
        var loaded = await readContext.Connections.FindAsync(conn.Id);
        loaded!.ValidatedAt.Should().NotBeNull();
    }
}
