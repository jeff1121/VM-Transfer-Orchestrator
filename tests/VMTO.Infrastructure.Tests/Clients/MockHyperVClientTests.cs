using FluentAssertions;
using VMTO.Infrastructure.Clients;

namespace VMTO.Infrastructure.Tests.Clients;

public sealed class MockHyperVClientTests
{
    private readonly MockHyperVClient _client = new();

    [Fact]
    public async Task ListVmsAsyncShouldReturnMockVmList()
    {
        var result = await _client.ListVmsAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value!.Count.Should().Be(2);
    }

    [Fact]
    public async Task GetVmStateAsyncShouldReturnOffState()
    {
        var result = await _client.GetVmStateAsync(Guid.NewGuid(), "hv-vm-01");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Off");
    }

    [Fact]
    public async Task ExportVhdxAsyncShouldReturnStream()
    {
        var result = await _client.ExportDiskAsync(Guid.NewGuid(), "hv-vm-01", "disk-0");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Length.Should().Be(1024);
    }

    [Fact]
    public async Task GetVmDetailsAsyncShouldReturnDetails()
    {
        var result = await _client.InspectAsync(Guid.NewGuid(), "hv-vm-01");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be("hv-vm-01");
        result.Value.State.Should().Be("Off");
    }

    [Fact]
    public async Task RunPreFlightCheckAsyncShouldReturnAllPassed()
    {
        var result = await _client.RunPreFlightCheckAsync(Guid.NewGuid(), "hv-vm-01");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.IsAllPassed.Should().BeTrue();
        result.Value.Items.Should().HaveCount(5);
    }
}
