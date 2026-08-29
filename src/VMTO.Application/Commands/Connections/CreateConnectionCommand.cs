using VMTO.Domain.Aggregates.Connection;

namespace VMTO.Application.Commands.Connections;

public sealed record CreateConnectionCommand(
    string Name,
    PlatformKind Type,
    string Endpoint,
    string Secret,
    string? MetadataJson = null);
