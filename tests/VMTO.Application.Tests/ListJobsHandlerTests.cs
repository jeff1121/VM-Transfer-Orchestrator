using FluentAssertions;
using NSubstitute;
using VMTO.Application.DTOs;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Queries.Handlers;
using VMTO.Application.Queries.Jobs;
using VMTO.Domain.Aggregates.Artifact;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.ValueObjects;

namespace VMTO.Application.Tests;

/// <summary>
/// ListJobsHandler 的單元測試。
/// 驗證列出工作清單的邏輯。
/// </summary>
public sealed class ListJobsHandlerTests
{
    private readonly IJobRepository _jobRepository = Substitute.For<IJobRepository>();
    private readonly ListJobsHandler _handler;

    private static readonly StorageTarget DefaultStorage = new(StorageType.S3, "http://localhost:9000", "bucket", "us-east-1");
    private static readonly MigrationOptions DefaultOptions = new(ArtifactFormat.Qcow2, false, true, 3);

    public ListJobsHandlerTests()
    {
        _handler = new ListJobsHandler(_jobRepository);
    }

    [Fact]
    public async Task HandleAsync_成功列出Jobs()
    {
        var job1 = new MigrationJob(Guid.NewGuid(), Guid.NewGuid(), DefaultStorage, MigrationStrategy.FullCopy, DefaultOptions);
        var job2 = new MigrationJob(Guid.NewGuid(), Guid.NewGuid(), DefaultStorage, MigrationStrategy.Incremental, DefaultOptions);
        _jobRepository.ListAsync(1, 10, null, Arg.Any<CancellationToken>())
            .Returns(new List<MigrationJob> { job1, job2 });

        var result = await _handler.HandleAsync(new ListJobsQuery(1, 10, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value![0].Id.Should().Be(job1.Id);
        result.Value[1].Id.Should().Be(job2.Id);
    }
}
