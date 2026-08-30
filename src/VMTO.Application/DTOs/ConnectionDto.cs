using VMTO.Domain.Aggregates.Connection;

namespace VMTO.Application.DTOs;

public sealed record ConnectionDto(
    Guid Id,
    string Name,
    PlatformKind Type,
    string Endpoint,
    DateTime? ValidatedAt,
    DateTime CreatedAt);
