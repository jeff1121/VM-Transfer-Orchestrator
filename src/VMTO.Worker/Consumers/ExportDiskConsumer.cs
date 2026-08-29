using MassTransit;
using Microsoft.Extensions.Logging;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Enums;
using VMTO.Infrastructure.Storage;
using VMTO.Worker.Messages;
using VMTO.Worker.Telemetry;

namespace VMTO.Worker.Consumers;

public sealed partial class ExportDiskConsumer(
    IJobRepository jobRepository,
    IConnectionRepository connectionRepository,
    ISourcePlatformProviderFactory sourceFactory,
    StorageAdapterFactory storageFactory,
    INotificationService notifications,
    ILogger<ExportDiskConsumer> logger) : IConsumer<ExportDiskMessage>
{
    public async Task Consume(ConsumeContext<ExportDiskMessage> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        LogStarting(logger, msg.JobId, msg.StepId);

        var job = await jobRepository.GetByIdAsync(msg.JobId, ct);
        if (job is null)
        {
            LogJobNotFound(logger, msg.JobId);
            return;
        }

        var step = job.Steps.FirstOrDefault(s => s.Id == msg.StepId);
        if (step is null)
        {
            LogStepNotFound(logger, msg.StepId, msg.JobId);
            return;
        }

        using var telemetry = WorkerTracing.StartStepActivity(
            nameof(ExportDiskConsumer), step, msg.JobId, msg.StepId, msg.CorrelationId);

        step.Start();
        await jobRepository.UpdateAsync(job, ct);
        await notifications.SendStepProgressAsync(msg.JobId, msg.StepId, 0, StepStatus.Running, ct);
        await using var heartbeat = StepHeartbeat.Start(
            token => notifications.SendStepProgressAsync(msg.JobId, msg.StepId, step.Progress, StepStatus.Running, token),
            TimeSpan.FromSeconds(15),
            logger,
            nameof(ExportDiskConsumer),
            ct);

        try
        {
            var connection = await connectionRepository.GetByIdAsync(msg.SourceConnectionId, ct);
            if (connection is null)
            {
                await FailStepAsync(job, step, "Source connection not found", msg, context, ct);
                return;
            }

            var source = sourceFactory.GetProvider(connection.Type);
            var stateResult = await source.GetVmStateAsync(msg.SourceConnectionId, msg.VmId, ct);
            if (!stateResult.IsSuccess)
            {
                await FailStepAsync(job, step, stateResult.ErrorMessage ?? "Failed to check VM state", msg, context, ct);
                return;
            }

            if (!IsOffline(stateResult.Value))
            {
                await FailStepAsync(job, step,
                    $"VM {msg.VmId} is not offline (current state: {stateResult.Value}). Offline export requires the VM to be powered off.",
                    msg, context, ct);
                return;
            }

            var progress = new Progress<int>(percent =>
            {
                step.UpdateProgress(percent);
                _ = notifications.SendStepProgressAsync(msg.JobId, msg.StepId, percent, StepStatus.Running, ct);
            });

            var exportResult = await source.ExportDiskAsync(msg.SourceConnectionId, msg.VmId, msg.DiskKey, progress, ct);
            if (!exportResult.IsSuccess)
            {
                await FailStepAsync(job, step, exportResult.ErrorMessage ?? "Export failed", msg, context, ct);
                return;
            }

            await using var stream = exportResult.Value!;
            var storageKey = $"jobs/{msg.JobId}/export/{msg.DiskKey}";
            var storage = storageFactory.Create(job.StorageTarget.Type);
            var uploadResult = await storage.UploadAsync(storageKey, stream, stream.Length, "application/octet-stream", ct);
            if (!uploadResult.IsSuccess)
            {
                await FailStepAsync(job, step, uploadResult.ErrorMessage ?? "Upload failed", msg, context, ct);
                return;
            }

            step.Complete();
            job.UpdateProgress();
            await jobRepository.UpdateAsync(job, ct);
            await notifications.SendStepProgressAsync(msg.JobId, msg.StepId, 100, StepStatus.Succeeded, ct);

            await context.Publish(new StepCompletedMessage(
                msg.JobId, msg.StepId, step.Name, msg.CorrelationId,
                new Dictionary<string, string>
                {
                    ["ExportedStorageKey"] = storageKey,
                    [$"ExportedStorageKey:{msg.DiskKey}"] = storageKey
                }), ct);
        }
        catch (Exception ex)
        {
            LogFailed(logger, ex, msg.JobId, msg.StepId);
            await FailStepAsync(job, step, ex.Message, msg, context, ct);
        }
    }

    private static bool IsOffline(string? state) =>
        state is null
        || state.Equals("Off", StringComparison.OrdinalIgnoreCase)
        || state.Equals("ShutDown", StringComparison.OrdinalIgnoreCase)
        || state.Equals("poweredOff", StringComparison.OrdinalIgnoreCase)
        || state.Equals("PoweredOff", StringComparison.OrdinalIgnoreCase);

    private async Task FailStepAsync(
        Domain.Aggregates.MigrationJob.MigrationJob job,
        Domain.Aggregates.MigrationJob.JobStep step,
        string error,
        ExportDiskMessage msg,
        ConsumeContext<ExportDiskMessage> context,
        CancellationToken ct)
    {
        await ConsumerHelper.FailStepAsync(
            job, step, error, msg.JobId, msg.StepId, step.Name, msg.CorrelationId,
            context, jobRepository, notifications, ct);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ExportDisk starting for Job {JobId}, Step {StepId}")]
    private static partial void LogStarting(ILogger logger, Guid jobId, Guid stepId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Job {JobId} not found")]
    private static partial void LogJobNotFound(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Step {StepId} not found in Job {JobId}")]
    private static partial void LogStepNotFound(ILogger logger, Guid stepId, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "ExportDisk failed for Job {JobId}, Step {StepId}")]
    private static partial void LogFailed(ILogger logger, Exception ex, Guid jobId, Guid stepId);
}
