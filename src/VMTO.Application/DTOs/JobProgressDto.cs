using VMTO.Domain.Enums;

namespace VMTO.Application.DTOs;

public sealed record JobProgressDto(
    Guid JobId,
    JobStatus Status,
    int OverallProgress,
    IReadOnlyList<JobStepDto> Steps);
