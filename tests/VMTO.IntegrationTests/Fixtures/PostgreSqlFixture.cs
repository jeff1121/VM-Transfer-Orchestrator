using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using VMTO.Infrastructure.Persistence;

namespace VMTO.IntegrationTests.Fixtures;

/// <summary>
/// PostgreSQL Testcontainer 共用 fixture，用於整合測試。
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("vmto_test")
        .WithUsername("vmto")
        .WithPassword("vmto_test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // 自動執行 EF Core 遷移
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
