using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;

namespace VMTO.Domain.ValueObjects;

public sealed record MigrationPlan(
    MigrationStrategy Strategy,
    IReadOnlyList<MigrationStepType> Steps);
