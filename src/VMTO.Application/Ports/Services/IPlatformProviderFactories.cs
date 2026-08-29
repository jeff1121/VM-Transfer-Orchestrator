using VMTO.Domain.Aggregates.Connection;

namespace VMTO.Application.Ports.Services;

public interface ISourcePlatformProviderFactory
{
    IVmSourcePort GetProvider(PlatformKind type);
}

public interface ITargetPlatformProviderFactory
{
    IVmTargetPort GetProvider(PlatformKind type);
}

public interface IPlatformAdapterRegistry
{
    IVmSourcePort GetSource(PlatformKind type);
    IVmTargetPort GetTarget(PlatformKind type);
    void EnsureRegistered();
}
