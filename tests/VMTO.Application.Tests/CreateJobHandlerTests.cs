using FluentAssertions;
using NSubstitute;
using VMTO.Application.Commands.Handlers;
using VMTO.Application.Commands.Jobs;
using VMTO.Application.DTOs;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.Artifact;
using VMTO.Domain.Aggregates.Connection;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;
using VMTO.Domain.ValueObjects;
using VMTO.Shared;

namespace VMTO.Application.Tests;

public sealed class CreateJobHandlerTests
{
    private readonly IJobRepository _jobRepository = Substitute.For<IJobRepository>();
    private readonly IConnectionRepository _connectionRepository = Substitute.For<IConnectionRepository>();
    private readonly ISourcePlatformProviderFactory _sourceFactory = Substitute.For<ISourcePlatformProviderFactory>();
    private readonly CreateJobHandler _handler;

    private static readonly StorageTarget DefaultStorage = new(StorageType.S3, "http://localhost:9000", "bucket", "us-east-1");
    private static readonly MigrationOptions DefaultOptions = new(ArtifactFormat.Qcow2, true, 3);

    public CreateJobHandlerTests()
    {
        _handler = new CreateJobHandler(_jobRepository, _connectionRepository, _sourceFactory);
    }

    [Fact]
    public async Task HandleAsync_VSphereFullCopy_BuildsNeutralPlan()
    {
        var source = new Connection("vc", PlatformKind.VSphere, "https://vc", new EncryptedSecret("c", "k"));
        var target = new Connection("pve", PlatformKind.ProxmoxVE, "https://pve", new EncryptedSecret("c", "k"));
        _connectionRepository.GetByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        _connectionRepository.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);

        MigrationJob? saved = null;
        await _jobRepository.AddAsync(Arg.Do<MigrationJob>(j => saved = j), Arg.Any<CancellationToken>());

        var result = await _handler.HandleAsync(new CreateJobCommand(
            source.Id, target.Id, DefaultStorage, MigrationStrategy.FullCopy, DefaultOptions, "vm-101", ["disk-0"]));

        result.IsSuccess.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.Steps.Select(s => s.StepType).Should().Equal(
            MigrationStepKind.ExportDisk,
            MigrationStepKind.ConvertDisk,
            MigrationStepKind.StageArtifact,
            MigrationStepKind.ProvisionTargetVm,
            MigrationStepKind.AttachDisk,
            MigrationStepKind.ConfigureTargetVm,
            MigrationStepKind.VerifyTargetVm,
            MigrationStepKind.Cleanup);
        saved.Strategy.Should().Be(MigrationStrategy.FullCopy);
        saved.VmId.Should().Be("vm-101");
    }

    [Fact]
    public async Task HandleAsync_HyperVIncremental_Rejected()
    {
        var source = new Connection("hv", PlatformKind.HyperV, "https://hv", new EncryptedSecret("c", "k"));
        var target = new Connection("pve", PlatformKind.ProxmoxVE, "https://pve", new EncryptedSecret("c", "k"));
        _connectionRepository.GetByIdAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        _connectionRepository.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);

        var result = await _handler.HandleAsync(new CreateJobCommand(
            source.Id, target.Id, DefaultStorage, MigrationStrategy.Incremental, DefaultOptions, "hv-vm-01", ["disk-0"]));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Plan.IncrementalNotSupported);
    }

    [Fact]
    public async Task HandleAsync_MissingVmId_Fails()
    {
        var result = await _handler.HandleAsync(new CreateJobCommand(
            Guid.NewGuid(), Guid.NewGuid(), DefaultStorage, MigrationStrategy.FullCopy, DefaultOptions, ""));

        result.IsSuccess.Should().BeFalse();
    }
}
