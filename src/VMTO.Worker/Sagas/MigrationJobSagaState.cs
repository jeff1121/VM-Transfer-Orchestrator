using MassTransit;

namespace VMTO.Worker.Sagas;

public sealed class MigrationJobSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    public Guid JobId { get; set; }
    public int CurrentStepIndex { get; set; }
    public List<string> StepNames { get; set; } = [];
    public List<Guid> StepIds { get; set; } = [];
    public Guid SourceConnectionId { get; set; }
    public Guid TargetConnectionId { get; set; }
    public string VmId { get; set; } = string.Empty;
    public string DiskKey { get; set; } = string.Empty;
    public string TargetFormat { get; set; } = "qcow2";
    public string VmName { get; set; } = string.Empty;
    public int Cores { get; set; }
    public int MemoryMb { get; set; }
    public Dictionary<string, string> StepOutputData { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
