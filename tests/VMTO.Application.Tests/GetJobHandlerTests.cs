using FluentAssertions;
using NSubstitute;
using VMTO.Application.DTOs;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Queries.Handlers;
using VMTO.Application.Queries.Jobs;
using VMTO.Domain.Aggregates.Artifact;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;
using VMTO.Domain.ValueObjects;
using VMTO.Shared;

namespace VMTO.Application.Tests;

/// <summary>
/// GetJobHandler 的單元測試。
/// 驗證取得工作詳情的各種情境。
/// </summary>
public sealed class GetJobHandlerTests
{
    private readonly IJobRepository _jobRepository = Substitute.For<IJobRepository>();
    private readonly GetJobHandler _handler;

    private static readonly StorageTarget DefaultStorage = new(StorageType.S3, "http://localhost:9000", "bucket", "us-east-1");
    private static readonly MigrationOptions DefaultOptions = new(ArtifactFormat.Qcow2, false, true, 3);

    public GetJobHandlerTests()
    {
        _handler = new GetJobHandler(_jobRepository);
    }

    [Fact]
    public async Task HandleAsync_找不到Job回傳錯誤()
    {
        var jobId = Guid.NewGuid();
        _jobRepository.GetByIdAsync(jobId, Arg.Any<CancellationToken>()).Returns((MigrationJob?)null);

        var result = await _handler.HandleAsync(new GetJobQuery(jobId));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.NotFound);
    }

    [Fact]
    public async Task HandleAsync_成功取得並映射JobDto()
    {
        var job = new MigrationJob(Guid.NewGuid(), Guid.NewGuid(), DefaultStorage, MigrationStrategy.FullCopy, DefaultOptions);
        job.AddStep("ExportVmdk", 1);
        job.AddStep("ConvertDisk", 2);
        _jobRepository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await _handler.HandleAsync(new GetJobQuery(job.Id));

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!;
        dto.Id.Should().Be(job.Id);
        dto.CorrelationId.Should().Be(job.CorrelationId.Value);
        dto.Strategy.Should().Be(MigrationStrategy.FullCopy);
        dto.Status.Should().Be(JobStatus.Created);
        dto.Steps.Should().HaveCount(2);
        dto.Steps[0].Name.Should().Be("ExportVmdk");
        dto.Steps[1].Name.Should().Be("ConvertDisk");
    }
}
