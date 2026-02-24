namespace VMTO.Domain.Strategies;

public sealed class FullCopyStrategy : IMigrationStrategy
{
    public IReadOnlyList<string> GetStepNames() =>
        ["ExportVmdk", "ConvertDisk", "UploadArtifact", "ImportToPve", "Verify"];
}
