using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.Connection;

namespace VMTO.Application.Ports.Services;

public interface ISourcePlatformProviderFactory
{
    ISourcePlatformPort GetProvider(ConnectionType type);
}

public interface ITargetPlatformProviderFactory
{
    ITargetPlatformPort GetProvider(ConnectionType type);
}
