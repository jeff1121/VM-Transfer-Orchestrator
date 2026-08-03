using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.Artifact;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;
using VMTO.Domain.ValueObjects;
using VMTO.Infrastructure.Storage;
using VMTO.Shared;
using VMTO.Worker.Consumers;
using VMTO.Worker.Messages;
using VMTO.Worker.Sagas;

namespace VMTO.Worker.Tests;

public sealed class ExportVhdxConsumerTests
{
    private readonly IJobRepository _jobRepository = Substitute.For<IJobRepository>();
    private readonly IHyperVClient _hyperVClient = Substitute.For<IHyperVClient>();
    private readonly StorageAdapterFactory _storageFactory = Substitute.For<StorageAdapterFactory>();
    private readonly IStorageAdapter _storageAdapter = Substitute.For<IStorageAdapter>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    [Fact]
    public async Task Consume_WhenVmIsOff_ExportsVhdxAndPublishesCompletedMessage()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<ExportVhdxConsumer>();
            })
            .AddSingleton(_jobRepository)
            .AddSingleton(_hyperVClient)
            .AddSingleton(_storageFactory)
            .AddSingleton(_notifications)
            .AddSingleton(NullLogger<ExportVhdxConsumer>.Instance)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var jobId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var connId = Guid.NewGuid();

        var job = new MigrationJob(
            connId, Guid.NewGuid(),
            new StorageTarget(StorageType.S3, "http://localhost:9000", "bucket"),
            MigrationStrategy.HyperVOffline,
            new MigrationOptions(ArtifactFormat.Qcow2, false, true, 3));
        job.AddStep("ExportVhdx", 1);
        var step = job.Steps[0];
        typeof(JobStep).GetProperty("Id")!.SetValue(step, stepId);

        _jobRepository.GetByIdAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);
        _hyperVClient.GetVmStateAsync(connId, "hv-vm-01", Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("Off"));

        var memoryStream = new MemoryStream(new byte[1024]);
        _hyperVClient.ExportVhdxAsync(connId, "hv-vm-01", "disk-0", Arg.Any<IProgress<int>>(), Arg.Any<CancellationToken>())
            .Returns(Result<Stream>.Success(memoryStream));

        _storageFactory.Create(StorageType.S3).Returns(_storageAdapter);
        _storageAdapter.UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await harness.Bus.Publish(new ExportVhdxMessage(jobId, stepId, connId, "hv-vm-01", "disk-0", Guid.NewGuid()));

        (await harness.Published.Any<StepCompletedMessage>()).Should().BeTrue();
    }
}

public sealed class MigrationJobSagaHyperVTests
{
    [Fact]
    public async Task JobStarted_WithHyperVOfflineStrategy_PublishesExportVhdxMessage()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<MigrationJobSaga, MigrationJobSagaState>()
                 .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var jobId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        await harness.Bus.Publish(new JobStartedMessage(
            jobId,
            ["ExportVhdx", "ConvertDisk", "UploadArtifact", "ImportToPve", "Verify"],
            [stepId],
            Guid.NewGuid(),
            Guid.NewGuid(),
            "http://minio:9000",
            "bucket",
            correlationId,
            "hv-vm-01",
            "disk-0"));

        var sagaHarness = harness.GetSagaStateMachineHarness<MigrationJobSaga, MigrationJobSagaState>();
        await harness.InactivityTask;
        (await sagaHarness.Consumed.Any<JobStartedMessage>()).Should().BeTrue();
        (await sagaHarness.Created.Any(x => x.CorrelationId == correlationId)).Should().BeTrue();

        var instance = sagaHarness.Created.Contains(correlationId);
        instance.Should().NotBeNull();
        instance.CurrentState.Should().Be("Exporting");
    }
}
