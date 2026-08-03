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

public sealed class HyperVClient : IHyperVClient
{
    private readonly HttpClient _http;
    private readonly ResiliencePipeline _pipeline;
    private readonly IChaosPolicy _chaosPolicy;

    public HyperVClient(
        HttpClient http,
        CircuitBreakerNotifier notifier,
        IOptions<RetryPolicyOptions> retryOptions,
        IChaosPolicy chaosPolicy)
    {
        _http = http;
        _chaosPolicy = chaosPolicy;
        _pipeline = CircuitBreakerPipelineFactory.Create(
            serviceName: "hyperv",
            minimumThroughput: 5,
            breakDuration: TimeSpan.FromSeconds(30),
            retryOptions: retryOptions.Value,
            retryClassifier: RetryClassifier.IsVsphereRetryable,
            notifier);
    }

    public async Task<Result<IReadOnlyList<VmInfoDto>>> ListVmsAsync(Guid connectionId, CancellationToken ct = default)
    {
        using var activity = ActivitySources.Default.StartActivity("hyperv.list_vms", ActivityKind.Client);
        activity?.SetTag("vmto.connection.id", connectionId.ToString());

        try
        {
            await _chaosPolicy.ApplyAsync("hyperv.list_vms", ct);
            return await _pipeline.ExecuteAsync(async token =>
            {
                var response = await _http.GetAsync($"/api/hyperv/vms", token);
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
                ErrorCodes.General.ExternalCommandFailed, $"Hyper-V request timed out: {ex.Message}");
        }
        catch (BrokenCircuitException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<IReadOnlyList<VmInfoDto>>.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"Hyper-V circuit breaker is open: {ex.Message}");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<IReadOnlyList<VmInfoDto>>.Failure(
                ErrorCodes.General.InternalError, $"Failed to list Hyper-V VMs: {ex.Message}");
        }
    }

    public async Task<Result<string>> GetVmStateAsync(Guid connectionId, string vmId, CancellationToken ct = default)
    {
        using var activity = ActivitySources.Default.StartActivity("hyperv.get_vm_state", ActivityKind.Client);
        activity?.SetTag("vmto.connection.id", connectionId.ToString());
        activity?.SetTag("vmto.vm.id", vmId);

        try
        {
            await _chaosPolicy.ApplyAsync("hyperv.get_vm_state", ct);
            return await _pipeline.ExecuteAsync(async token =>
            {
                var response = await _http.GetAsync($"/api/hyperv/vm/{vmId}/state", token);
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                response.EnsureSuccessStatusCode();

                var state = await response.Content.ReadAsStringAsync(token);
                return Result<string>.Success(state);
            }, ct);
        }
        catch (TimeoutRejectedException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<string>.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"Hyper-V request timed out: {ex.Message}");
        }
        catch (BrokenCircuitException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<string>.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"Hyper-V circuit breaker is open: {ex.Message}");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<string>.Failure(
                ErrorCodes.General.InternalError, $"Failed to get Hyper-V VM state: {ex.Message}");
        }
    }

    public async Task<Result<HyperVVmDetailsDto>> GetVmDetailsAsync(Guid connectionId, string vmId, CancellationToken ct = default)
    {
        using var activity = ActivitySources.Default.StartActivity("hyperv.get_vm_details", ActivityKind.Client);
        activity?.SetTag("vmto.connection.id", connectionId.ToString());
        activity?.SetTag("vmto.vm.id", vmId);

        try
        {
            await _chaosPolicy.ApplyAsync("hyperv.get_vm_details", ct);
            return await _pipeline.ExecuteAsync(async token =>
            {
                var response = await _http.GetAsync($"/api/hyperv/vm/{vmId}/details", token);
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                response.EnsureSuccessStatusCode();

                var details = await response.Content.ReadFromJsonAsync<HyperVVmDetailsDto>(token);
                if (details is null)
                    return Result<HyperVVmDetailsDto>.Failure(ErrorCodes.General.InternalError, "Failed to parse VM details.");

                return Result<HyperVVmDetailsDto>.Success(details);
            }, ct);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<HyperVVmDetailsDto>.Failure(
                ErrorCodes.General.InternalError, $"Failed to get Hyper-V VM details: {ex.Message}");
        }
    }

    public async Task<Result<PreFlightCheckResultDto>> RunPreFlightCheckAsync(Guid connectionId, string vmId, CancellationToken ct = default)
    {
        using var activity = ActivitySources.Default.StartActivity("hyperv.run_preflight", ActivityKind.Client);
        activity?.SetTag("vmto.connection.id", connectionId.ToString());
        activity?.SetTag("vmto.vm.id", vmId);

        try
        {
            await _chaosPolicy.ApplyAsync("hyperv.run_preflight", ct);
            return await _pipeline.ExecuteAsync(async token =>
            {
                var response = await _http.PostAsync($"/api/hyperv/vm/{vmId}/preflight", null, token);
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<PreFlightCheckResultDto>(token);
                if (result is null)
                    return Result<PreFlightCheckResultDto>.Failure(ErrorCodes.General.InternalError, "Failed to parse preflight check result.");

                return Result<PreFlightCheckResultDto>.Success(result);
            }, ct);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<PreFlightCheckResultDto>.Failure(
                ErrorCodes.General.InternalError, $"Failed to run preflight check: {ex.Message}");
        }
    }

    public async Task<Result<Stream>> ExportVhdxAsync(Guid connectionId, string vmId, string diskKey, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        using var activity = ActivitySources.Default.StartActivity("hyperv.export_vhdx", ActivityKind.Client);
        activity?.SetTag("vmto.connection.id", connectionId.ToString());
        activity?.SetTag("vmto.vm.id", vmId);
        activity?.SetTag("vmto.disk.key", diskKey);

        try
        {
            await _chaosPolicy.ApplyAsync("hyperv.export_vhdx", ct);
            return await _pipeline.ExecuteAsync(async token =>
            {
                var response = await _http.GetAsync($"/api/hyperv/vm/{vmId}/disk/{diskKey}/export", token);
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
                ErrorCodes.General.ExternalCommandFailed, $"Hyper-V request timed out: {ex.Message}");
        }
        catch (BrokenCircuitException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<Stream>.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"Hyper-V circuit breaker is open: {ex.Message}");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<Stream>.Failure(
                ErrorCodes.General.InternalError, $"Failed to export VHDX: {ex.Message}");
        }
    }

    public Task<Result<Stream>> ExportDiskAsync(Guid connectionId, string vmId, string diskKey, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        return ExportVhdxAsync(connectionId, vmId, diskKey, progress, ct);
    }
}
