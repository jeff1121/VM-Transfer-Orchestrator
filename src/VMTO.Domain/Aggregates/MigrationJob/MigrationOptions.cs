using VMTO.Domain.Aggregates.Artifact;

namespace VMTO.Domain.Aggregates.MigrationJob;

public sealed record MigrationOptions(
    ArtifactFormat TargetDiskFormat,
    bool DeleteSourceAfter,
    bool VerifyChecksum,
    int MaxRetries);
