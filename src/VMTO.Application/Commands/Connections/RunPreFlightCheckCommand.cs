using VMTO.Application.DTOs;

namespace VMTO.Application.Commands.Connections;

public sealed record RunPreFlightCheckCommand(Guid ConnectionId, string VmId);
