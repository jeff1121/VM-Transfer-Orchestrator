using MassTransit;
using VMTO.Worker.Messages;

namespace VMTO.Worker.Sagas;

public sealed class MigrationJobSaga : MassTransitStateMachine<MigrationJobSagaState>
{
    // States
    public State Exporting { get; private set; } = null!;
    public State Converting { get; private set; } = null!;
    public State Uploading { get; private set; } = null!;
    public State Importing { get; private set; } = null!;
    public State Verifying { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;
    public State Cancelled { get; private set; } = null!;

    // Events
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

        // Initial -> Exporting: publish ExportVmdkMessage for the first step
        Initially(
            When(JobStarted)
                .Then(ctx =>
                {
                    ctx.Saga.JobId = ctx.Message.JobId;
                    ctx.Saga.StepNames = ctx.Message.StepNames;
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
                    ctx.Saga.CreatedAt = DateTime.UtcNow;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .PublishAsync(ctx => ctx.Init<ExportVmdkMessage>(new ExportVmdkMessage(
                    ctx.Saga.JobId,
                    ctx.Saga.StepIds.Count > 0 ? ctx.Saga.StepIds[0] : Guid.Empty,
                    ctx.Saga.SourceConnectionId,
                    ctx.Saga.VmId,
                    ctx.Saga.DiskKey,
                    ctx.Saga.CorrelationId)))
                .TransitionTo(Exporting));

        // Exporting -> Converting on step completed
        During(Exporting,
            When(StepCompleted)
                .Then(ctx =>
                {
                    foreach (var kv in ctx.Message.OutputData ?? [])
                        ctx.Saga.StepOutputData[kv.Key] = kv.Value;
                    ctx.Saga.CurrentStepIndex++;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .PublishAsync(ctx => ctx.Init<ConvertDiskMessage>(new ConvertDiskMessage(
                    ctx.Saga.JobId,
                    ctx.Saga.StepIds.Count > ctx.Saga.CurrentStepIndex ? ctx.Saga.StepIds[ctx.Saga.CurrentStepIndex] : Guid.Empty,
                    ctx.Saga.StepOutputData.GetValueOrDefault("ExportedStorageKey", $"jobs/{ctx.Saga.JobId}/export/{ctx.Saga.DiskKey}.vmdk"),
                    $"jobs/{ctx.Saga.JobId}/convert/output.{ctx.Saga.TargetFormat}",
                    ctx.Saga.TargetFormat,
                    ctx.Saga.CorrelationId)))
                .TransitionTo(Converting),
            When(StepFailed)
                .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                .TransitionTo(Failed),
            When(JobCancelRequested)
                .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                .TransitionTo(Cancelled));

        // Converting -> Uploading
        During(Converting,
            When(StepCompleted)
                .Then(ctx =>
                {
                    foreach (var kv in ctx.Message.OutputData ?? [])
                        ctx.Saga.StepOutputData[kv.Key] = kv.Value;
                    ctx.Saga.CurrentStepIndex++;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .PublishAsync(ctx => ctx.Init<UploadArtifactMessage>(new UploadArtifactMessage(
                    ctx.Saga.JobId,
                    ctx.Saga.StepIds.Count > ctx.Saga.CurrentStepIndex ? ctx.Saga.StepIds[ctx.Saga.CurrentStepIndex] : Guid.Empty,
                    ctx.Saga.StepOutputData.GetValueOrDefault("ConvertedOutputPath", $"jobs/{ctx.Saga.JobId}/convert/output.{ctx.Saga.TargetFormat}"),
                    $"jobs/{ctx.Saga.JobId}/artifacts/output.{ctx.Saga.TargetFormat}",
                    ctx.Saga.CorrelationId)))
                .TransitionTo(Uploading),
            When(StepFailed)
                .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                .TransitionTo(Failed),
            When(JobCancelRequested)
                .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                .TransitionTo(Cancelled));

        // Uploading -> Importing
        During(Uploading,
            When(StepCompleted)
                .Then(ctx =>
                {
                    foreach (var kv in ctx.Message.OutputData ?? [])
                        ctx.Saga.StepOutputData[kv.Key] = kv.Value;
                    ctx.Saga.CurrentStepIndex++;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .PublishAsync(ctx => ctx.Init<ImportToPveMessage>(new ImportToPveMessage(
                    ctx.Saga.JobId,
                    ctx.Saga.StepIds.Count > ctx.Saga.CurrentStepIndex ? ctx.Saga.StepIds[ctx.Saga.CurrentStepIndex] : Guid.Empty,
                    ctx.Saga.TargetConnectionId,
                    ctx.Saga.StepOutputData.GetValueOrDefault("ArtifactStorageKey", string.Empty),
                    ctx.Saga.TargetFormat,
                    ctx.Saga.VmName,
                    ctx.Saga.Cores,
                    ctx.Saga.MemoryMb,
                    ctx.Saga.CorrelationId)))
                .TransitionTo(Importing),
            When(StepFailed)
                .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                .TransitionTo(Failed),
            When(JobCancelRequested)
                .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                .TransitionTo(Cancelled));

        // Importing -> Verifying
        During(Importing,
            When(StepCompleted)
                .Then(ctx =>
                {
                    foreach (var kv in ctx.Message.OutputData ?? [])
                        ctx.Saga.StepOutputData[kv.Key] = kv.Value;
                    ctx.Saga.CurrentStepIndex++;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .PublishAsync(ctx => ctx.Init<VerifyMessage>(new VerifyMessage(
                    ctx.Saga.JobId,
                    ctx.Saga.StepIds.Count > ctx.Saga.CurrentStepIndex ? ctx.Saga.StepIds[ctx.Saga.CurrentStepIndex] : Guid.Empty,
                    Guid.TryParse(ctx.Saga.StepOutputData.GetValueOrDefault("ArtifactId"), out var aid) ? aid : Guid.Empty,
                    ctx.Saga.StepOutputData.GetValueOrDefault("Checksum", string.Empty),
                    ctx.Saga.CorrelationId)))
                .TransitionTo(Verifying),
            When(StepFailed)
                .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                .TransitionTo(Failed),
            When(JobCancelRequested)
                .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                .TransitionTo(Cancelled));

        // Verifying -> Completed
        During(Verifying,
            When(StepCompleted)
                .Then(ctx =>
                {
                    ctx.Saga.CurrentStepIndex++;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .TransitionTo(Completed)
                .Finalize(),
            When(StepFailed)
                .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                .TransitionTo(Failed),
            When(JobCancelRequested)
                .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                .TransitionTo(Cancelled));
    }
}

public sealed record JobCancelRequestedMessage(Guid JobId, Guid CorrelationId);
