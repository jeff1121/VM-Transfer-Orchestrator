namespace VMTO.Domain.Enums;

public enum MigrationStepKind
{
    ExportDisk,
    ConvertDisk,
    StageArtifact,
    ProvisionTargetVm,
    AttachDisk,
    ConfigureTargetVm,
    VerifyTargetVm,
    Cleanup,
    EnableCbt,
    IncrementalPull,
    ApplyDelta,
    FinalSyncCutover
}
