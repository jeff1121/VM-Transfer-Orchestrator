using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;
using VMTO.Domain.ValueObjects;

namespace VMTO.Domain.Strategies;

public sealed class FullCopyStrategy : IMigrationStrategy
{
    public MigrationPlan GetPlan() =>
        new(MigrationStrategy.FullCopy,
        [
            MigrationStepType.ExportVmdk,
            MigrationStepType.ConvertDisk,
            MigrationStepType.UploadArtifact,
            MigrationStepType.ImportToPve,
            MigrationStepType.Verify
        ]);
}
