using FluentAssertions;
using NSubstitute;
using VMTO.Application.DTOs;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Queries.Artifacts;
using VMTO.Application.Queries.Handlers;
using VMTO.Domain.Aggregates.Artifact;
using VMTO.Domain.ValueObjects;

namespace VMTO.Application.Tests;

/// <summary>
/// GetArtifactsHandler 的單元測試。
/// 驗證列出成品清單的邏輯。
/// </summary>
public sealed class GetArtifactsHandlerTests
{
    private readonly IArtifactRepository _artifactRepository = Substitute.For<IArtifactRepository>();
    private readonly GetArtifactsHandler _handler;

    public GetArtifactsHandlerTests()
    {
        _handler = new GetArtifactsHandler(_artifactRepository);
    }

    [Fact]
    public async Task HandleAsync_成功列出Artifacts()
    {
        var jobId = Guid.NewGuid();
        var artifact = new Artifact(jobId, "disk.qcow2", ArtifactFormat.Qcow2,
            new Checksum("SHA256", "abc123"), 1024 * 1024, "s3://bucket/disk.qcow2");
        _artifactRepository.ListByJobIdAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(new List<Artifact> { artifact });

        var result = await _handler.HandleAsync(new GetArtifactsQuery(jobId));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        var dto = result.Value![0];
        dto.FileName.Should().Be("disk.qcow2");
        dto.Format.Should().Be(ArtifactFormat.Qcow2);
        dto.ChecksumAlgorithm.Should().Be("SHA256");
        dto.ChecksumValue.Should().Be("abc123");
        dto.SizeBytes.Should().Be(1024 * 1024);
    }
}
