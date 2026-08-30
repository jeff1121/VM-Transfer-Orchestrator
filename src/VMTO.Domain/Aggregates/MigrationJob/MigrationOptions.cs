using VMTO.Domain.Aggregates.Artifact;

namespace VMTO.Domain.Aggregates.MigrationJob;

public sealed record MigrationOptions(
    ArtifactFormat TargetDiskFormat,
    bool VerifyChecksum,
    int MaxRetries);
