using VMTO.Domain.Aggregates.Connection;
using VMTO.Domain.Enums;

namespace VMTO.Domain.ValueObjects;

public sealed record PlannedStep(
    MigrationStepKind Kind,
    int Order,
    IReadOnlyDictionary<string, string> Input)
{
    public string DiskKey => Input.TryGetValue("diskKey", out var value) ? value : string.Empty;
}

public sealed record MigrationPlan(
    int Version,
    PlatformKind SourcePlatform,
    PlatformKind TargetPlatform,
    string SourceAdapterId,
    string TargetAdapterId,
    IReadOnlyList<PlannedStep> Steps);
