using VMTO.Domain.Aggregates.Connection;

namespace VMTO.Application.Commands.Connections;

public sealed record CreateConnectionCommand(
    string Name,
    ConnectionType Type,
    string Endpoint,
    string Secret);
