using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;
using VMTO.Domain.ValueObjects;

namespace VMTO.Domain.Strategies;

public sealed class IncrementalStrategy : IMigrationStrategy
{
    public MigrationPlan GetPlan() =>
        new(MigrationStrategy.Incremental,
        [
            MigrationStepType.EnableCbt,
            MigrationStepType.IncrementalPull,
            MigrationStepType.ApplyDelta,
            MigrationStepType.FinalSyncCutover,
            MigrationStepType.Verify
        ]);
}
