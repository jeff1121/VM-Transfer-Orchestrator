using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;
using VMTO.Domain.ValueObjects;

namespace VMTO.Domain.Strategies;

public sealed class HyperVOfflineExportStrategy : IMigrationStrategy
{
    public MigrationPlan GetPlan() =>
        new(MigrationStrategy.HyperVOffline,
        [
            MigrationStepType.ExportVhdx,
            MigrationStepType.ConvertDisk,
            MigrationStepType.UploadArtifact,
            MigrationStepType.ImportToPve,
            MigrationStepType.Verify
        ]);
}
