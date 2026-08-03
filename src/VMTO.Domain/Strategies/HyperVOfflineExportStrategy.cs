namespace VMTO.Domain.Strategies;

public sealed class HyperVOfflineExportStrategy : IMigrationStrategy
{
    public IReadOnlyList<string> GetStepNames() =>
        ["ExportVhdx", "ConvertDisk", "UploadArtifact", "ImportToPve", "Verify"];
}
