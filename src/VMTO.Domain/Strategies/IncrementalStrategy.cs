namespace VMTO.Domain.Strategies;

public sealed class IncrementalStrategy : IMigrationStrategy
{
    public IReadOnlyList<string> GetStepNames() =>
        ["EnableCbt", "IncrementalPull", "ApplyDelta", "FinalSyncCutover", "Verify"];
}
