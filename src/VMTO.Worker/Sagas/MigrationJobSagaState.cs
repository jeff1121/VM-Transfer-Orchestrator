using MassTransit;

namespace VMTO.Worker.Sagas;

public sealed class MigrationJobSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    public Guid JobId { get; set; }
    public int CurrentStepIndex { get; set; }
    public List<string> StepNames { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
