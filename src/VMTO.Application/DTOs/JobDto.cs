using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;

namespace VMTO.Application.DTOs;

public sealed record JobDto(
    Guid Id,
    string CorrelationId,
    MigrationStrategy Strategy,
    JobStatus Status,
    int Progress,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<JobStepDto> Steps);
