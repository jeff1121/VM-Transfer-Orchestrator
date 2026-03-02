using FluentAssertions;
using NSubstitute;
using VMTO.Application.Commands.Handlers;
using VMTO.Application.Commands.Jobs;
using VMTO.Application.Ports.Repositories;
using VMTO.Domain.Aggregates.Artifact;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.ValueObjects;

namespace VMTO.Application.Tests;

/// <summary>
/// CreateJobHandler 的單元測試。
/// 驗證工作建立及步驟產生邏輯。
/// </summary>
public sealed class CreateJobHandlerTests
{
    private readonly IJobRepository _jobRepository = Substitute.For<IJobRepository>();
    private readonly CreateJobHandler _handler;

    private static readonly StorageTarget DefaultStorage = new(StorageType.S3, "http://localhost:9000", "bucket", "us-east-1");
    private static readonly MigrationOptions DefaultOptions = new(ArtifactFormat.Qcow2, false, true, 3);

    public CreateJobHandlerTests()
    {
        _handler = new CreateJobHandler(_jobRepository);
    }

    [Fact]
    public async Task HandleAsync_成功建立Job並回傳Id()
    {
        var command = new CreateJobCommand(
            Guid.NewGuid(), Guid.NewGuid(), DefaultStorage, MigrationStrategy.FullCopy, DefaultOptions);

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        await _jobRepository.Received(1).AddAsync(Arg.Any<MigrationJob>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_正確建立FullCopy策略的步驟()
    {
        MigrationJob? savedJob = null;
        await _jobRepository.AddAsync(Arg.Do<MigrationJob>(j => savedJob = j), Arg.Any<CancellationToken>());

        var command = new CreateJobCommand(
            Guid.NewGuid(), Guid.NewGuid(), DefaultStorage, MigrationStrategy.FullCopy, DefaultOptions);

        await _handler.HandleAsync(command);

        savedJob.Should().NotBeNull();
        savedJob!.Steps.Should().HaveCount(5);
        savedJob.Steps.Select(s => s.Name).Should()
            .Equal("ExportVmdk", "ConvertDisk", "UploadArtifact", "ImportToPve", "Verify");
        savedJob.Steps.Select(s => s.Order).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task HandleAsync_正確建立Incremental策略的步驟()
    {
        MigrationJob? savedJob = null;
        await _jobRepository.AddAsync(Arg.Do<MigrationJob>(j => savedJob = j), Arg.Any<CancellationToken>());

        var command = new CreateJobCommand(
            Guid.NewGuid(), Guid.NewGuid(), DefaultStorage, MigrationStrategy.Incremental, DefaultOptions);

        await _handler.HandleAsync(command);

        savedJob.Should().NotBeNull();
        savedJob!.Steps.Should().HaveCount(5);
        savedJob.Steps.Select(s => s.Name).Should()
            .Equal("EnableCbt", "IncrementalPull", "ApplyDelta", "FinalSyncCutover", "Verify");
    }
}
