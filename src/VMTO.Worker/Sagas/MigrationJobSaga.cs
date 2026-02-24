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

        // Initial -> Exporting
        Initially(
            When(JobStarted)
                .Then(ctx =>
                {
                    ctx.Saga.JobId = ctx.Message.JobId;
                    ctx.Saga.StepNames = ctx.Message.StepNames;
                    ctx.Saga.CurrentStepIndex = 0;
                    ctx.Saga.CreatedAt = DateTime.UtcNow;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .TransitionTo(Exporting));

        // Exporting -> Converting on step completed
        During(Exporting,
            When(StepCompleted)
                .Then(ctx =>
                {
                    ctx.Saga.CurrentStepIndex++;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
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
                    ctx.Saga.CurrentStepIndex++;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
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
                    ctx.Saga.CurrentStepIndex++;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
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
                    ctx.Saga.CurrentStepIndex++;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
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
