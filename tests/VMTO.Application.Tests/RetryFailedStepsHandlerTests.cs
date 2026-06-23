using FluentAssertions;
using NSubstitute;
using VMTO.Application.Commands.Handlers;
using VMTO.Application.Commands.Jobs;
using VMTO.Application.Ports.Repositories;
using VMTO.Domain.Aggregates.Artifact;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;
using VMTO.Domain.ValueObjects;
using VMTO.Shared;

namespace VMTO.Application.Tests;

/// <summary>
/// RetryFailedStepsHandler 的單元測試。
/// 驗證重試失敗步驟的各種情境。
/// </summary>
public sealed class RetryFailedStepsHandlerTests
{
    private readonly IJobRepository _jobRepository = Substitute.For<IJobRepository>();
    private readonly RetryFailedStepsHandler _handler;

    private static readonly StorageTarget DefaultStorage = new(StorageType.S3, "http://localhost:9000", "bucket", "us-east-1");
    private static readonly MigrationOptions DefaultOptions = new(ArtifactFormat.Qcow2, false, true, 3);

    public RetryFailedStepsHandlerTests()
    {
        _handler = new RetryFailedStepsHandler(_jobRepository);
    }

    [Fact]
    public async Task HandleAsync_找不到Job回傳錯誤()
    {
        var jobId = Guid.NewGuid();
        _jobRepository.GetByIdAsync(jobId, Arg.Any<CancellationToken>()).Returns((MigrationJob?)null);

        var result = await _handler.HandleAsync(new RetryFailedStepsCommand(jobId));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.NotFound);
    }

    [Fact]
    public async Task HandleAsync_成功重試失敗的步驟()
    {
        var job = new MigrationJob(Guid.NewGuid(), Guid.NewGuid(), DefaultStorage, MigrationStrategy.FullCopy, DefaultOptions);
        job.AddStep("ExportVmdk", 1);
        job.AddStep("ConvertDisk", 2);

        // 將第一個步驟推進到 Failed 狀態
        job.Steps[0].Start();
        job.Steps[0].Fail("測試錯誤");

        _jobRepository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await _handler.HandleAsync(new RetryFailedStepsCommand(job.Id));

        result.IsSuccess.Should().BeTrue();
        job.Steps[0].Status.Should().Be(StepStatus.Retrying);
        job.Steps[0].RetryCount.Should().Be(1);
        job.Steps[1].Status.Should().Be(StepStatus.Pending);
        await _jobRepository.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_沒有失敗步驟時直接成功()
    {
        var job = new MigrationJob(Guid.NewGuid(), Guid.NewGuid(), DefaultStorage, MigrationStrategy.FullCopy, DefaultOptions);
        job.AddStep("ExportVmdk", 1);

        _jobRepository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await _handler.HandleAsync(new RetryFailedStepsCommand(job.Id));

        result.IsSuccess.Should().BeTrue();
        await _jobRepository.DidNotReceive().UpdateAsync(Arg.Any<MigrationJob>(), Arg.Any<CancellationToken>());
    }
}
