namespace VMTO.Domain.Enums;

public enum MigrationStepType
{
    ExportVmdk,
    ExportVhdx,
    ConvertDisk,
    UploadArtifact,
    ImportToPve,
    Verify,
    EnableCbt,
    IncrementalPull,
    ApplyDelta,
    FinalSyncCutover
}
