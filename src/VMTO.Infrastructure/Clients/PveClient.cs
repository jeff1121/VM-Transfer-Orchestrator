using System.Net.Http.Json;
using VMTO.Application.Ports.Services;
using VMTO.Shared;

namespace VMTO.Infrastructure.Clients;

public sealed class PveClient : IPveClient
{
    private readonly HttpClient _http;

    public PveClient(HttpClient http) => _http = http;

    public async Task<Result<int>> CreateVmAsync(Guid connectionId, string vmName, int cores, int memoryMb, CancellationToken ct = default)
    {
        try
        {
            var payload = new { name = vmName, cores, memory = memoryMb };
            var response = await _http.PostAsJsonAsync("/api2/json/nodes/pve/qemu", payload, ct);
            response.EnsureSuccessStatusCode();

            // Placeholder: parse VMID from response
            return Result<int>.Success(100);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure(
                ErrorCodes.General.InternalError, $"Failed to create VM: {ex.Message}");
        }
    }

    public async Task<Result> ImportDiskAsync(Guid connectionId, int vmId, string storageUri, string diskFormat, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        try
        {
            var payload = new { storage = storageUri, format = diskFormat };
            var response = await _http.PostAsJsonAsync($"/api2/json/nodes/pve/qemu/{vmId}/disk/import", payload, ct);
            response.EnsureSuccessStatusCode();

            progress?.Report(100);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(
                ErrorCodes.General.InternalError, $"Failed to import disk: {ex.Message}");
        }
    }

    public async Task<Result> ConfigureVmAsync(Guid connectionId, int vmId, Dictionary<string, string> settings, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"/api2/json/nodes/pve/qemu/{vmId}/config", settings, ct);
            response.EnsureSuccessStatusCode();

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(
                ErrorCodes.General.InternalError, $"Failed to configure VM: {ex.Message}");
        }
    }
}
