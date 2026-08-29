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

    public IVmSourcePort GetProvider(PlatformKind type)
    {
        return type switch
        {
            PlatformKind.VSphere => _vSphereClient,
            PlatformKind.HyperV => _hyperVClient,
            _ => throw new NotSupportedException($"PlatformKind '{type}' is not supported as a source platform.")
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

    public IVmTargetPort GetProvider(PlatformKind type)
    {
        return type switch
        {
            PlatformKind.ProxmoxVE => _pveClient,
            _ => throw new NotSupportedException($"PlatformKind '{type}' is not supported as a target platform.")
        };
    }
}

public sealed class PlatformAdapterRegistry : IPlatformAdapterRegistry
{
    private readonly ISourcePlatformProviderFactory _sources;
    private readonly ITargetPlatformProviderFactory _targets;

    public PlatformAdapterRegistry(
        ISourcePlatformProviderFactory sources,
        ITargetPlatformProviderFactory targets)
    {
        _sources = sources;
        _targets = targets;
    }

    public IVmSourcePort GetSource(PlatformKind type) => _sources.GetProvider(type);

    public IVmTargetPort GetTarget(PlatformKind type) => _targets.GetProvider(type);

    public void EnsureRegistered()
    {
        _ = GetSource(PlatformKind.VSphere);
        _ = GetSource(PlatformKind.HyperV);
        _ = GetTarget(PlatformKind.ProxmoxVE);
    }
}
