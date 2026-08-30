using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using VMTO.Application.Messages;
using VMTO.Domain.Enums;
using VMTO.Worker.Messages;
using VMTO.Worker.Sagas;

namespace VMTO.Worker.Tests;

public sealed class MigrationJobSagaPlanTests
{
    [Fact]
    public async Task JobStarted_WithExportDiskFirstStep_PublishesExportDiskMessage()
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

        var correlationId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        await harness.Bus.Publish(new JobStartedMessage(
            Guid.NewGuid(),
            ["ExportDisk", "ConvertDisk"],
            [stepId, Guid.NewGuid()],
            Guid.NewGuid(),
            Guid.NewGuid(),
            "http://minio:9000",
            "bucket",
            correlationId,
            "vm-101",
            "disk-0",
            StepTypes: [MigrationStepKind.ExportDisk, MigrationStepKind.ConvertDisk],
            SourcePlatform: "VSphere",
            TargetPlatform: "ProxmoxVE",
            StepDiskKeys: ["disk-0", "disk-0"]));

        var sagaHarness = harness.GetSagaStateMachineHarness<MigrationJobSaga, MigrationJobSagaState>();
        await harness.InactivityTask;
        (await sagaHarness.Consumed.Any<JobStartedMessage>()).Should().BeTrue();
        (await harness.Published.Any<ExportDiskMessage>()).Should().BeTrue();
    }

    [Fact]
    public async Task JobStarted_HyperVSource_StillPublishesGenericExportDisk()
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

        var correlationId = Guid.NewGuid();
        await harness.Bus.Publish(new JobStartedMessage(
            Guid.NewGuid(),
            ["ExportDisk"],
            [Guid.NewGuid()],
            Guid.NewGuid(),
            Guid.NewGuid(),
            "http://minio:9000",
            "bucket",
            correlationId,
            "hv-vm-01",
            "disk-0",
            StepTypes: [MigrationStepKind.ExportDisk],
            SourcePlatform: "HyperV",
            TargetPlatform: "ProxmoxVE",
            StepDiskKeys: ["disk-0"]));

        await harness.InactivityTask;
        (await harness.Published.Any<ExportDiskMessage>()).Should().BeTrue();
    }
}
