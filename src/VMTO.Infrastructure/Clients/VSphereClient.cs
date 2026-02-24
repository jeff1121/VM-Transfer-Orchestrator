using System.Net.Http.Json;
using VMTO.Application.DTOs;
using VMTO.Application.Ports.Services;
using VMTO.Shared;

namespace VMTO.Infrastructure.Clients;

public sealed class VSphereClient : IVSphereClient
{
    private readonly HttpClient _http;

    public VSphereClient(HttpClient http) => _http = http;

    public async Task<Result<IReadOnlyList<VmInfoDto>>> ListVmsAsync(Guid connectionId, CancellationToken ct = default)
    {
        try
        {
            // Placeholder: real implementation would authenticate and call vSphere REST API
            var response = await _http.GetAsync($"/api/vcenter/vm", ct);
            response.EnsureSuccessStatusCode();

            var vms = await response.Content.ReadFromJsonAsync<List<VmInfoDto>>(ct)
                ?? [];
            return Result<IReadOnlyList<VmInfoDto>>.Success(vms);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<VmInfoDto>>.Failure(
                ErrorCodes.General.InternalError, $"Failed to list VMs: {ex.Message}");
        }
    }

    public async Task<Result<Stream>> ExportVmdkAsync(Guid connectionId, string vmId, string diskKey, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"/api/vcenter/vm/{vmId}/disk/{diskKey}/export", ct);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync(ct);
            return Result<Stream>.Success(stream);
        }
        catch (Exception ex)
        {
            return Result<Stream>.Failure(
                ErrorCodes.General.InternalError, $"Failed to export VMDK: {ex.Message}");
        }
    }

    public async Task<Result<bool>> IsCbtEnabledAsync(Guid connectionId, string vmId, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"/api/vcenter/vm/{vmId}", ct);
            response.EnsureSuccessStatusCode();

            // Placeholder: parse CBT status from response
            return Result<bool>.Success(false);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(
                ErrorCodes.General.InternalError, $"Failed to check CBT: {ex.Message}");
        }
    }

    public async Task<Result> EnableCbtAsync(Guid connectionId, string vmId, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsync($"/api/vcenter/vm/{vmId}/cbt/enable", null, ct);
            response.EnsureSuccessStatusCode();

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(
                ErrorCodes.General.InternalError, $"Failed to enable CBT: {ex.Message}");
        }
    }
}
