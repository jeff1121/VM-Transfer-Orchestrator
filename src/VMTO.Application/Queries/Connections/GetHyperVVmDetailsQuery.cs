using VMTO.Application.DTOs;

namespace VMTO.Application.Queries.Connections;

public sealed record GetHyperVVmDetailsQuery(Guid ConnectionId, string VmId);
public sealed record ListVmsQuery(Guid ConnectionId);
