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
/// CancelJobHandler 的單元測試。
/// 驗證取消工作的各種情境。
/// </summary>
public sealed class CancelJobHandlerTests
{
    private readonly IJobRepository _jobRepository = Substitute.For<IJobRepository>();
    private readonly CancelJobHandler _handler;

    private static readonly StorageTarget DefaultStorage = new(StorageType.S3, "http://localhost:9000", "bucket", "us-east-1");
    private static readonly MigrationOptions DefaultOptions = new(ArtifactFormat.Qcow2, false, true, 3);

    public CancelJobHandlerTests()
    {
        _handler = new CancelJobHandler(_jobRepository);
    }

    [Fact]
    public async Task HandleAsync_找不到Job回傳錯誤()
    {
        var jobId = Guid.NewGuid();
        _jobRepository.GetByIdAsync(jobId, Arg.Any<CancellationToken>()).Returns((MigrationJob?)null);

        var result = await _handler.HandleAsync(new CancelJobCommand(jobId));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.NotFound);
    }

    [Fact]
    public async Task HandleAsync_成功取消Running的Job()
    {
        var job = CreateJobInStatus(JobStatus.Running);
        _jobRepository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await _handler.HandleAsync(new CancelJobCommand(job.Id));

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Cancelling);
        await _jobRepository.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_無法取消Created狀態的Job()
    {
        var job = new MigrationJob(Guid.NewGuid(), Guid.NewGuid(), DefaultStorage, MigrationStrategy.FullCopy, DefaultOptions);
        _jobRepository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await _handler.HandleAsync(new CancelJobCommand(job.Id));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    private static MigrationJob CreateJobInStatus(JobStatus status)
    {
        var job = new MigrationJob(Guid.NewGuid(), Guid.NewGuid(), DefaultStorage, MigrationStrategy.FullCopy, DefaultOptions);
        if (status == JobStatus.Created) return job;
        job.Enqueue();
        if (status == JobStatus.Queued) return job;
        job.Start();
        if (status == JobStatus.Running) return job;
        return job;
    }
}
