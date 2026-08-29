using MassTransit;
using VMTO.Application.Messages;
using VMTO.Domain.Enums;
using VMTO.Worker.Messages;

namespace VMTO.Worker.Sagas;

public sealed class MigrationJobSaga : MassTransitStateMachine<MigrationJobSagaState>
{
    public State Executing { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;
    public State Cancelled { get; private set; } = null!;

    public Event<JobStartedMessage> JobStarted { get; private set; } = null!;
    public Event<StepCompletedMessage> StepCompleted { get; private set; } = null!;
    public Event<StepFailedMessage> StepFailed { get; private set; } = null!;
    public Event<JobCancelRequestedMessage> JobCancelRequested { get; private set; } = null!;

    public MigrationJobSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => JobStarted, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => StepCompleted, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => StepFailed, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => JobCancelRequested, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));

        Initially(
            When(JobStarted)
                .Then(ctx =>
                {
                    ctx.Saga.JobId = ctx.Message.JobId;
                    ctx.Saga.StepNames = ctx.Message.StepNames;
                    ctx.Saga.StepTypes = ctx.Message.StepTypes ?? [];
                    ctx.Saga.StepIds = ctx.Message.StepIds;
                    ctx.Saga.CurrentStepIndex = 0;
                    ctx.Saga.SourceConnectionId = ctx.Message.SourceConnectionId;
                    ctx.Saga.TargetConnectionId = ctx.Message.TargetConnectionId;
                    ctx.Saga.VmId = ctx.Message.VmId;
                    ctx.Saga.DiskKey = ctx.Message.DiskKey;
                    ctx.Saga.TargetFormat = ctx.Message.TargetFormat;
                    ctx.Saga.VmName = ctx.Message.VmName;
                    ctx.Saga.Cores = ctx.Message.Cores;
                    ctx.Saga.MemoryMb = ctx.Message.MemoryMb;
                    ctx.Saga.SourcePlatform = ctx.Message.SourcePlatform;
                    ctx.Saga.TargetPlatform = ctx.Message.TargetPlatform;
                    ctx.Saga.StepDiskKeys = ctx.Message.StepDiskKeys ?? [];
                    ctx.Saga.CreatedAt = DateTime.UtcNow;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .ThenAsync(ctx => MigrationStepPublisher.PublishCurrentAsync(ctx))
                .TransitionTo(Executing));

        During(Executing,
            When(StepCompleted)
                .Then(ctx =>
                {
                    foreach (var kv in ctx.Message.OutputData ?? [])
                        ctx.Saga.StepOutputData[kv.Key] = kv.Value;
                    ctx.Saga.CurrentStepIndex++;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .IfElse(
                    ctx => ctx.Saga.CurrentStepIndex < ctx.Saga.StepTypes.Count,
                    passed => passed.ThenAsync(ctx => MigrationStepPublisher.PublishCurrentAsync(ctx)),
                    remaining => remaining.TransitionTo(Completed).Finalize()),
            When(StepFailed)
                .ThenAsync(ctx => ctx.Publish(new TargetRollbackMessage(
                    ctx.Saga.JobId,
                    ctx.Saga.TargetConnectionId,
                    ctx.Saga.StepOutputData.GetValueOrDefault("TargetVmId"),
                    ctx.Saga.CorrelationId)))
                .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                .TransitionTo(Failed),
            When(JobCancelRequested)
                .ThenAsync(ctx => ctx.Publish(new TargetRollbackMessage(
                    ctx.Saga.JobId,
                    ctx.Saga.TargetConnectionId,
                    ctx.Saga.StepOutputData.GetValueOrDefault("TargetVmId"),
                    ctx.Saga.CorrelationId)))
                .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                .TransitionTo(Cancelled));
    }
}

public sealed record JobCancelRequestedMessage(Guid JobId, Guid CorrelationId);

internal static class MigrationStepPublisher
{
    public static Task PublishCurrentAsync(BehaviorContext<MigrationJobSagaState> ctx)
    {
        var saga = ctx.Saga;
        var kind = saga.StepTypes[saga.CurrentStepIndex];
        var stepId = saga.StepIds.Count > saga.CurrentStepIndex ? saga.StepIds[saga.CurrentStepIndex] : Guid.Empty;
        var diskKey = DiskKeyFor(saga, saga.CurrentStepIndex);

        return kind switch
        {
            MigrationStepKind.ExportDisk => ctx.Publish(new ExportDiskMessage(
                saga.JobId, stepId, saga.SourceConnectionId, saga.VmId, diskKey, saga.CorrelationId)),
            MigrationStepKind.ConvertDisk => ctx.Publish(new ConvertDiskMessage(
                saga.JobId, stepId,
                saga.StepOutputData.GetValueOrDefault($"ExportedStorageKey:{diskKey}",
                    saga.StepOutputData.GetValueOrDefault("ExportedStorageKey", $"jobs/{saga.JobId}/export/{diskKey}")),
                $"jobs/{saga.JobId}/convert/{diskKey}.{saga.TargetFormat}",
                saga.TargetFormat,
                saga.CorrelationId)),
            MigrationStepKind.StageArtifact => ctx.Publish(new UploadArtifactMessage(
                saga.JobId, stepId,
                saga.StepOutputData.GetValueOrDefault($"ConvertedOutputPath:{diskKey}",
                    saga.StepOutputData.GetValueOrDefault("ConvertedOutputPath", $"jobs/{saga.JobId}/convert/{diskKey}.{saga.TargetFormat}")),
                $"jobs/{saga.JobId}/artifacts/{diskKey}.{saga.TargetFormat}",
                saga.CorrelationId)),
            MigrationStepKind.ProvisionTargetVm => ctx.Publish(new ProvisionTargetVmMessage(
                saga.JobId, stepId, saga.TargetConnectionId, saga.VmName, saga.Cores, saga.MemoryMb, saga.CorrelationId)),
            MigrationStepKind.AttachDisk => ctx.Publish(new AttachDiskMessage(
                saga.JobId, stepId, saga.TargetConnectionId,
                saga.StepOutputData.GetValueOrDefault("TargetVmId", string.Empty),
                saga.StepOutputData.GetValueOrDefault($"ArtifactStorageKey:{diskKey}",
                    saga.StepOutputData.GetValueOrDefault("ArtifactStorageKey", string.Empty)),
                saga.TargetFormat, diskKey, saga.CorrelationId)),
            MigrationStepKind.ConfigureTargetVm => ctx.Publish(new ConfigureTargetVmMessage(
                saga.JobId, stepId, saga.TargetConnectionId,
                saga.StepOutputData.GetValueOrDefault("TargetVmId", string.Empty),
                saga.CorrelationId)),
            MigrationStepKind.VerifyTargetVm => ctx.Publish(new VerifyMessage(
                saga.JobId, stepId,
                Guid.TryParse(saga.StepOutputData.GetValueOrDefault("ArtifactId"), out var aid) ? aid : Guid.Empty,
                saga.StepOutputData.GetValueOrDefault("Checksum", string.Empty),
                saga.CorrelationId)),
            MigrationStepKind.Cleanup => ctx.Publish(new CleanupMessage(
                saga.JobId, stepId, saga.SourceConnectionId, saga.VmId, saga.CorrelationId)),
            MigrationStepKind.EnableCbt => ctx.Publish(new EnableCbtMessage(saga.JobId, stepId, saga.SourceConnectionId, saga.VmId, saga.CorrelationId)),
            MigrationStepKind.IncrementalPull => ctx.Publish(new IncrementalPullMessage(
                saga.JobId, stepId, saga.SourceConnectionId, saga.VmId,
                saga.StepOutputData.GetValueOrDefault("ChangeId", string.Empty),
                saga.StepOutputData.GetValueOrDefault("BaseStorageKey", string.Empty),
                saga.CorrelationId)),
            MigrationStepKind.ApplyDelta => ctx.Publish(new ApplyDeltaMessage(
                saga.JobId, stepId,
                saga.StepOutputData.GetValueOrDefault("DeltaStorageKey", string.Empty),
                saga.StepOutputData.GetValueOrDefault("TargetStorageKey", string.Empty),
                saga.CorrelationId)),
            MigrationStepKind.FinalSyncCutover => ctx.Publish(new FinalSyncCutoverMessage(
                saga.JobId, stepId, saga.SourceConnectionId, saga.TargetConnectionId, saga.VmId,
                int.TryParse(saga.StepOutputData.GetValueOrDefault("TargetVmId"), out var pveId) ? pveId : 0,
                saga.CorrelationId)),
            _ => Task.CompletedTask
        };
    }

    public static Task PublishCurrentAsync<T>(BehaviorContext<MigrationJobSagaState, T> ctx) where T : class
        => PublishCurrentAsync((BehaviorContext<MigrationJobSagaState>)ctx);

    private static string DiskKeyFor(MigrationJobSagaState saga, int index)
    {
        if (index >= 0 && index < saga.StepDiskKeys.Count && !string.IsNullOrEmpty(saga.StepDiskKeys[index]))
            return saga.StepDiskKeys[index];
        return saga.DiskKey;
    }
}
