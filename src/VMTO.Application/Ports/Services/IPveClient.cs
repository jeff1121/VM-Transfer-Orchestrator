using VMTO.Shared;

namespace VMTO.Application.Ports.Services;

public interface IPveClient : ITargetPlatformPort
{
    Task<Result> ConfigureVmAsync(Guid connectionId, int vmId, Dictionary<string, string> settings, CancellationToken ct = default);
}
