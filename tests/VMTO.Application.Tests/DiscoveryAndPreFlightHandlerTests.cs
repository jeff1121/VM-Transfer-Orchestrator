using FluentAssertions;
using NSubstitute;
using VMTO.Application.Commands.Connections;
using VMTO.Application.Commands.Handlers;
using VMTO.Application.DTOs;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Application.Queries.Connections;
using VMTO.Application.Queries.Handlers;
using VMTO.Domain.Aggregates.Connection;
using VMTO.Domain.ValueObjects;
using VMTO.Shared;

namespace VMTO.Application.Tests;

public sealed class DiscoveryAndPreFlightHandlerTests
{
    private readonly IConnectionRepository _connectionRepository = Substitute.For<IConnectionRepository>();
    private readonly IHyperVClient _hyperVClient = Substitute.For<IHyperVClient>();
    private readonly IVSphereClient _vSphereClient = Substitute.For<IVSphereClient>();

    [Fact]
    public async Task GetHyperVVmDetailsHandler_SuccessfulCheck_ReturnsVmDetails()
    {
        var connection = new Connection("hyperv-prod", ConnectionType.HyperV, "https://hyperv.local", new EncryptedSecret("cipher", "key-1"));
        _connectionRepository.GetByIdAsync(connection.Id, Arg.Any<CancellationToken>()).Returns(connection);

        var expectedDetails = new HyperVVmDetailsDto("hv-vm-01", "win-server-01", "Off", 4, 8192, "Windows Server 2022", 0, []);
        _hyperVClient.GetVmDetailsAsync(connection.Id, "hv-vm-01", Arg.Any<CancellationToken>())
            .Returns(Result<HyperVVmDetailsDto>.Success(expectedDetails));

        var handler = new GetHyperVVmDetailsHandler(_connectionRepository, _hyperVClient);
        var result = await handler.HandleAsync(new GetHyperVVmDetailsQuery(connection.Id, "hv-vm-01"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedDetails);
    }

    [Fact]
    public async Task RunPreFlightCheckHandler_SuccessfulCheck_ReturnsPreFlightResult()
    {
        var connection = new Connection("hyperv-prod", ConnectionType.HyperV, "https://hyperv.local", new EncryptedSecret("cipher", "key-1"));
        _connectionRepository.GetByIdAsync(connection.Id, Arg.Any<CancellationToken>()).Returns(connection);

        var expectedResult = new PreFlightCheckResultDto(connection.Id, "hv-vm-01", true, []);
        _hyperVClient.RunPreFlightCheckAsync(connection.Id, "hv-vm-01", Arg.Any<CancellationToken>())
            .Returns(Result<PreFlightCheckResultDto>.Success(expectedResult));

        var handler = new RunPreFlightCheckHandler(_connectionRepository, _hyperVClient);
        var result = await handler.HandleAsync(new RunPreFlightCheckCommand(connection.Id, "hv-vm-01"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedResult);
    }

    [Fact]
    public async Task ListVmsHandler_ForHyperVConnection_CallsProviderFactory()
    {
        var connection = new Connection("hyperv-prod", ConnectionType.HyperV, "https://hyperv.local", new EncryptedSecret("cipher", "key-1"));
        _connectionRepository.GetByIdAsync(connection.Id, Arg.Any<CancellationToken>()).Returns(connection);

        IReadOnlyList<VmInfoDto> expectedVms = [new VmInfoDto("hv-vm-01", "win-server-01", 4, 8192, ["disk-0"])];
        var sourceFactory = Substitute.For<ISourcePlatformProviderFactory>();
        var sourcePort = Substitute.For<ISourcePlatformPort>();

        sourceFactory.GetProvider(ConnectionType.HyperV).Returns(sourcePort);
        sourcePort.ListVmsAsync(connection.Id, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<VmInfoDto>>.Success(expectedVms));

        var handler = new ListVmsHandler(_connectionRepository, sourceFactory);
        var result = await handler.HandleAsync(new ListVmsQuery(connection.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedVms);
    }
}
