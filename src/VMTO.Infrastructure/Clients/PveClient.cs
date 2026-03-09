using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using VMTO.Application.Ports.Services;
using VMTO.Infrastructure.Resilience;
using VMTO.Shared;
using VMTO.Shared.Telemetry;

namespace VMTO.Infrastructure.Clients;

public sealed class PveClient : IPveClient
{
    private readonly HttpClient _http;
    private readonly ResiliencePipeline _pipeline;
    private readonly IChaosPolicy _chaosPolicy;

    public PveClient(
        HttpClient http,
        CircuitBreakerNotifier notifier,
        IOptions<RetryPolicyOptions> retryOptions,
        IChaosPolicy chaosPolicy)
    {
        _http = http;
        _chaosPolicy = chaosPolicy;
        _pipeline = CircuitBreakerPipelineFactory.Create(
            serviceName: "pve",
            minimumThroughput: 5,
            breakDuration: TimeSpan.FromSeconds(30),
            retryOptions: retryOptions.Value,
            retryClassifier: RetryClassifier.IsPveRetryable,
            notifier);
    }

    public async Task<Result<int>> CreateVmAsync(Guid connectionId, string vmName, int cores, int memoryMb, CancellationToken ct = default)
    {
        using var activity = ActivitySources.Default.StartActivity("pve.create_vm", ActivityKind.Client);
        activity?.SetTag("vmto.connection.id", connectionId.ToString());
        activity?.SetTag("vmto.vm.name", vmName);
        activity?.SetTag("vmto.vm.cores", cores);
        activity?.SetTag("vmto.vm.memory_mb", memoryMb);

        try
        {
            await _chaosPolicy.ApplyAsync("pve.create_vm", ct);
            return await _pipeline.ExecuteAsync(async token =>
            {
                var payload = new { name = vmName, cores, memory = memoryMb };
                var response = await _http.PostAsJsonAsync("/api2/json/nodes/pve/qemu", payload, token);
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                response.EnsureSuccessStatusCode();

                // Placeholder: parse VMID from response
                return Result<int>.Success(100);
            }, ct);
        }
        catch (TimeoutRejectedException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<int>.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"PVE request timed out: {ex.Message}");
        }
        catch (BrokenCircuitException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<int>.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"PVE circuit breaker is open: {ex.Message}");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<int>.Failure(
                ErrorCodes.General.InternalError, $"Failed to create VM: {ex.Message}");
        }
    }

    public async Task<Result> ImportDiskAsync(Guid connectionId, int vmId, string storageUri, string diskFormat, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        using var activity = ActivitySources.Default.StartActivity("pve.import_disk", ActivityKind.Client);
        activity?.SetTag("vmto.connection.id", connectionId.ToString());
        activity?.SetTag("vmto.vm.id", vmId);
        activity?.SetTag("vmto.storage.uri", storageUri);
        activity?.SetTag("vmto.disk.format", diskFormat);

        try
        {
            await _chaosPolicy.ApplyAsync("pve.import_disk", ct);
            return await _pipeline.ExecuteAsync(async token =>
            {
                var payload = new { storage = storageUri, format = diskFormat };
                var response = await _http.PostAsJsonAsync($"/api2/json/nodes/pve/qemu/{vmId}/disk/import", payload, token);
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                response.EnsureSuccessStatusCode();

                progress?.Report(100);
                return Result.Success();
            }, ct);
        }
        catch (TimeoutRejectedException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"PVE request timed out: {ex.Message}");
        }
        catch (BrokenCircuitException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"PVE circuit breaker is open: {ex.Message}");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result.Failure(
                ErrorCodes.General.InternalError, $"Failed to import disk: {ex.Message}");
        }
    }

    public async Task<Result> ConfigureVmAsync(Guid connectionId, int vmId, Dictionary<string, string> settings, CancellationToken ct = default)
    {
        using var activity = ActivitySources.Default.StartActivity("pve.configure_vm", ActivityKind.Client);
        activity?.SetTag("vmto.connection.id", connectionId.ToString());
        activity?.SetTag("vmto.vm.id", vmId);

        try
        {
            await _chaosPolicy.ApplyAsync("pve.configure_vm", ct);
            return await _pipeline.ExecuteAsync(async token =>
            {
                var response = await _http.PutAsJsonAsync($"/api2/json/nodes/pve/qemu/{vmId}/config", settings, token);
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                response.EnsureSuccessStatusCode();

                return Result.Success();
            }, ct);
        }
        catch (TimeoutRejectedException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"PVE request timed out: {ex.Message}");
        }
        catch (BrokenCircuitException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result.Failure(
                ErrorCodes.General.ExternalCommandFailed, $"PVE circuit breaker is open: {ex.Message}");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result.Failure(
                ErrorCodes.General.InternalError, $"Failed to configure VM: {ex.Message}");
        }
    }
}
