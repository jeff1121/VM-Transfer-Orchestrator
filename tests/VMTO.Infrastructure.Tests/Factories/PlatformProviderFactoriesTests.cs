using FluentAssertions;
using NSubstitute;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.Connection;
using VMTO.Infrastructure.Factories;

namespace VMTO.Infrastructure.Tests.Factories;

public sealed class PlatformProviderFactoriesTests
{
    [Fact]
    public void SourcePlatformProviderFactoryVSphereReturnsVSphereClient()
    {
        var vSphereClient = Substitute.For<IVSphereClient>();
        var hyperVClient = Substitute.For<IHyperVClient>();
        var factory = new SourcePlatformProviderFactory(vSphereClient, hyperVClient);

        var provider = factory.GetProvider(PlatformKind.VSphere);

        provider.Should().BeSameAs(vSphereClient);
    }

    [Fact]
    public void SourcePlatformProviderFactoryHyperVReturnsHyperVClient()
    {
        var vSphereClient = Substitute.For<IVSphereClient>();
        var hyperVClient = Substitute.For<IHyperVClient>();
        var factory = new SourcePlatformProviderFactory(vSphereClient, hyperVClient);

        var provider = factory.GetProvider(PlatformKind.HyperV);

        provider.Should().BeSameAs(hyperVClient);
    }

    [Fact]
    public void SourcePlatformProviderFactoryUnsupportedTypeThrowsNotSupportedException()
    {
        var vSphereClient = Substitute.For<IVSphereClient>();
        var hyperVClient = Substitute.For<IHyperVClient>();
        var factory = new SourcePlatformProviderFactory(vSphereClient, hyperVClient);

        Action act = () => factory.GetProvider(PlatformKind.ProxmoxVE);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void TargetPlatformProviderFactoryProxmoxVEReturnsPveClient()
    {
        var pveClient = Substitute.For<IPveClient>();
        var factory = new TargetPlatformProviderFactory(pveClient);

        var provider = factory.GetProvider(PlatformKind.ProxmoxVE);

        provider.Should().BeSameAs(pveClient);
    }

    [Fact]
    public void TargetPlatformProviderFactoryUnsupportedTypeThrowsNotSupportedException()
    {
        var pveClient = Substitute.For<IPveClient>();
        var factory = new TargetPlatformProviderFactory(pveClient);

        Action act = () => factory.GetProvider(PlatformKind.VSphere);

        act.Should().Throw<NotSupportedException>();
    }
}
