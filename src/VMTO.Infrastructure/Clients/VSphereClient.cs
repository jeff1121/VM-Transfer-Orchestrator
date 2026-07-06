using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using VMTO.Application.DTOs;
using VMTO.Application.Ports.Services;
using VMTO.Infrastructure.Resilience;
using VMTO.Shared;
using VMTO.Shared.Telemetry;

namespace VMTO.Infrastructure.Clients;

public sealed class VSphereClient : IVSphereClient
{
    private readonly HttpClient _http;
    private readonly ResiliencePipeline _pipeline;
    private readonly IChaosPolicy _chaosPolicy;

    public VSphereClient(
        HttpClient http,
        CircuitBreakerNotifier notifier,
        IOptions<RetryPolicyOptions> retryOptions,
        IChaosPolicy chaosPolicy)
    {
        _http = http;
        _chaosPolicy = chaosPolicy;
        _pipeline = CircuitBreakerPipelineFactory.Create(
            serviceName: "vsphere",
            minimumThroughput: 5,
            breakDuration: TimeSpan.FromSeconds(30),
            retryOptions: retryOptions.Value,
            retryClassifier: RetryClassifier.IsVsphereRetryable,
            notifier);
    }

    public async Task<Result<IReadOnlyList<VmInfoDto>>> ListVmsAsync(Guid connectionId, CancellationToken ct = default)
    {
        using var activity = ActivitySources.Default.StartActivity("vsphere.list_vms", ActivityKind.Client);
        activity?.SetTag("vmto.connection.id", connectionId.ToString());

        try
        {
            await _chaosPolicy.ApplyAsync("vsphere.list_vms", ct);
            return await _pipeline.ExecuteAsync(async token =>
            {
                var response = await _http.GetAsync($"/api/vcenter/vm", token);
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                response.EnsureSuccessStatusCode();

                var vms = await response.Content.ReadFromJsonAsync<List<VmInfoDto>>(token) ?? [];
                return Result<IReadOnlyList<VmInfoDto>>.Success(vms);
            }, ct);
        }
        catch (TimeoutRejectedException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<IReadOnlyList<VmInfoDto>>.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"vSphere request timed out: {ex.Message}");
        }
        catch (BrokenCircuitException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<IReadOnlyList<VmInfoDto>>.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"vSphere circuit breaker is open: {ex.Message}");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<IReadOnlyList<VmInfoDto>>.Failure(
                ErrorCodes.General.InternalError, $"Failed to list VMs: {ex.Message}");
        }
    }

    public async Task<Result<Stream>> ExportVmdkAsync(Guid connectionId, string vmId, string diskKey, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        using var activity = ActivitySources.Default.StartActivity("vsphere.export_vmdk", ActivityKind.Client);
        activity?.SetTag("vmto.connection.id", connectionId.ToString());
        activity?.SetTag("vmto.vm.id", vmId);
        activity?.SetTag("vmto.disk.key", diskKey);

        try
        {
            await _chaosPolicy.ApplyAsync("vsphere.export_vmdk", ct);
            return await _pipeline.ExecuteAsync(async token =>
            {
                var response = await _http.GetAsync($"/api/vcenter/vm/{vmId}/disk/{diskKey}/export", token);
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                response.EnsureSuccessStatusCode();

                var stream = await response.Content.ReadAsStreamAsync(token);
                return Result<Stream>.Success(stream);
            }, ct);
        }
        catch (TimeoutRejectedException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<Stream>.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"vSphere request timed out: {ex.Message}");
        }
        catch (BrokenCircuitException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<Stream>.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"vSphere circuit breaker is open: {ex.Message}");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<Stream>.Failure(
                ErrorCodes.General.InternalError, $"Failed to export VMDK: {ex.Message}");
        }
    }

    public async Task<Result<bool>> IsCbtEnabledAsync(Guid connectionId, string vmId, CancellationToken ct = default)
    {
        using var activity = ActivitySources.Default.StartActivity("vsphere.is_cbt_enabled", ActivityKind.Client);
        activity?.SetTag("vmto.connection.id", connectionId.ToString());
        activity?.SetTag("vmto.vm.id", vmId);

        try
        {
            await _chaosPolicy.ApplyAsync("vsphere.is_cbt_enabled", ct);
            return await _pipeline.ExecuteAsync(async token =>
            {
                var response = await _http.GetAsync($"/api/vcenter/vm/{vmId}", token);
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                response.EnsureSuccessStatusCode();

                // Placeholder: parse CBT status from response
                return Result<bool>.Success(false);
            }, ct);
        }
        catch (TimeoutRejectedException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<bool>.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"vSphere request timed out: {ex.Message}");
        }
        catch (BrokenCircuitException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<bool>.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"vSphere circuit breaker is open: {ex.Message}");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<bool>.Failure(
                ErrorCodes.General.InternalError, $"Failed to check CBT: {ex.Message}");
        }
    }

    public async Task<Result> EnableCbtAsync(Guid connectionId, string vmId, CancellationToken ct = default)
    {
        using var activity = ActivitySources.Default.StartActivity("vsphere.enable_cbt", ActivityKind.Client);
        activity?.SetTag("vmto.connection.id", connectionId.ToString());
        activity?.SetTag("vmto.vm.id", vmId);

        try
        {
            await _chaosPolicy.ApplyAsync("vsphere.enable_cbt", ct);
            return await _pipeline.ExecuteAsync(async token =>
            {
                var response = await _http.PostAsync($"/api/vcenter/vm/{vmId}/cbt/enable", null, token);
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                response.EnsureSuccessStatusCode();

                return Result.Success();
            }, ct);
        }
        catch (TimeoutRejectedException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"vSphere request timed out: {ex.Message}");
        }
        catch (BrokenCircuitException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"vSphere circuit breaker is open: {ex.Message}");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result.Failure(
                ErrorCodes.General.InternalError, $"Failed to enable CBT: {ex.Message}");
        }
    }
}
