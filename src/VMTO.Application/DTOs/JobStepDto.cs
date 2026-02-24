using VMTO.Domain.Enums;

namespace VMTO.Application.DTOs;

public sealed record JobStepDto(
    Guid Id,
    string Name,
    int Order,
    StepStatus Status,
    int Progress,
    int RetryCount,
    string? ErrorMessage);
