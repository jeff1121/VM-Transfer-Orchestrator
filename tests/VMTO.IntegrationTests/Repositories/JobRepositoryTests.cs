using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VMTO.Domain.Aggregates.Artifact;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;
using VMTO.Domain.ValueObjects;
using VMTO.Infrastructure.Persistence;
using VMTO.Infrastructure.Persistence.Repositories;
using VMTO.IntegrationTests.Fixtures;

namespace VMTO.IntegrationTests.Repositories;

/// <summary>
/// JobRepository 整合測試，使用真實 PostgreSQL 容器驗證儲存庫操作。
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class JobRepositoryTests(PostgreSqlFixture fixture)
{
    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options);

    [Fact]
    public async Task AddAsync_應成功新增_Job_至資料庫()
    {
        // Arrange
        await using var context = CreateContext();
        var repo = new JobRepository(context);
        var job = CreateTestJob();

        // Act
        await repo.AddAsync(job);

        // Assert — 確認已寫入資料庫
        var saved = await context.Jobs.FindAsync(job.Id);
        saved.Should().NotBeNull();
        saved!.Status.Should().Be(JobStatus.Created);
    }

    [Fact]
    public async Task GetByIdAsync_應取得含步驟的_Job()
    {
        // Arrange
        await using var context = CreateContext();
        var repo = new JobRepository(context);
        var job = CreateTestJob();
        job.AddStep("ExportVmdk", 1);
        job.AddStep("ConvertDisk", 2);
        await repo.AddAsync(job);

        // Act — 使用新 context 讀取，模擬不同請求
        await using var readContext = CreateContext();
        var readRepo = new JobRepository(readContext);
        var loaded = await readRepo.GetByIdAsync(job.Id);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Steps.Should().HaveCount(2);
        loaded.Steps[0].Name.Should().Be("ExportVmdk");
    }

    [Fact]
    public async Task UpdateAsync_應持久化狀態變更()
    {
        // Arrange
        await using var context = CreateContext();
        var repo = new JobRepository(context);
        var job = CreateTestJob();
        await repo.AddAsync(job);

        // Act — 將 Job 排入佇列
        job.Enqueue();
        await repo.UpdateAsync(job);

        // Assert — 驗證狀態已持久化
        await using var readContext = CreateContext();
        var loaded = await readContext.Jobs.FindAsync(job.Id);
        loaded!.Status.Should().Be(JobStatus.Queued);
    }

    [Fact]
    public async Task ListAsync_應支援分頁與狀態篩選()
    {
        // Arrange
        await using var context = CreateContext();
        var repo = new JobRepository(context);

        var job1 = CreateTestJob();
        var job2 = CreateTestJob();
        job2.Enqueue();
        await repo.AddAsync(job1);
        await repo.AddAsync(job2);

        // Act — 篩選 Queued 狀態
        var queued = await repo.ListAsync(1, 10, JobStatus.Queued);

        // Assert
        queued.Should().Contain(j => j.Id == job2.Id);
        queued.Should().NotContain(j => j.Id == job1.Id);
    }

    [Fact]
    public async Task CountAsync_應回傳正確數量()
    {
        // Arrange
        await using var context = CreateContext();
        var repo = new JobRepository(context);

        var initialCount = await repo.CountAsync();
        await repo.AddAsync(CreateTestJob());

        // Act
        var newCount = await repo.CountAsync();

        // Assert
        newCount.Should().Be(initialCount + 1);
    }

    /// <summary>
    /// 建立測試用 MigrationJob。
    /// </summary>
    private static MigrationJob CreateTestJob()
    {
        var storageTarget = new StorageTarget(StorageType.S3, "http://localhost:9000", "test-bucket");
        var options = new MigrationOptions(ArtifactFormat.Qcow2, false, true, 3);
        return new MigrationJob(Guid.NewGuid(), Guid.NewGuid(), storageTarget, MigrationStrategy.FullCopy, options);
    }
}
