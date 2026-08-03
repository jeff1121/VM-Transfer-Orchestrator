using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.Connection;

namespace VMTO.Infrastructure.Factories;

public sealed class SourcePlatformProviderFactory : ISourcePlatformProviderFactory
{
    private readonly IVSphereClient _vSphereClient;
    private readonly IHyperVClient _hyperVClient;

    public SourcePlatformProviderFactory(
        IVSphereClient vSphereClient,
        IHyperVClient hyperVClient)
    {
        _vSphereClient = vSphereClient;
        _hyperVClient = hyperVClient;
    }

    public ISourcePlatformPort GetProvider(ConnectionType type)
    {
        return type switch
        {
            ConnectionType.VSphere => _vSphereClient,
            ConnectionType.HyperV => _hyperVClient,
            _ => throw new NotSupportedException($"ConnectionType '{type}' is not supported as a source platform.")
        };
    }
}

public sealed class TargetPlatformProviderFactory : ITargetPlatformProviderFactory
{
    private readonly IPveClient _pveClient;

    public TargetPlatformProviderFactory(IPveClient pveClient)
    {
        _pveClient = pveClient;
    }

    public ITargetPlatformPort GetProvider(ConnectionType type)
    {
        return type switch
        {
            ConnectionType.ProxmoxVE => _pveClient,
            _ => throw new NotSupportedException($"ConnectionType '{type}' is not supported as a target platform.")
        };
    }
}
