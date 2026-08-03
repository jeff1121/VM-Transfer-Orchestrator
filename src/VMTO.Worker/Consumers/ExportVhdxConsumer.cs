using MassTransit;
using Microsoft.Extensions.Logging;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Enums;
using VMTO.Infrastructure.Storage;
using VMTO.Worker.Messages;
using VMTO.Worker.Telemetry;

namespace VMTO.Worker.Consumers;

public sealed partial class ExportVhdxConsumer(
    IJobRepository jobRepository,
    IHyperVClient hyperVClient,
    StorageAdapterFactory storageFactory,
    INotificationService notifications,
    ILogger<ExportVhdxConsumer> logger) : IConsumer<ExportVhdxMessage>
{
    public async Task Consume(ConsumeContext<ExportVhdxMessage> context)
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
            nameof(ExportVhdxConsumer), step, msg.JobId, msg.StepId, msg.CorrelationId);

        step.Start();
        await jobRepository.UpdateAsync(job, ct);
        await notifications.SendStepProgressAsync(msg.JobId, msg.StepId, 0, StepStatus.Running, ct);
        await using var heartbeat = StepHeartbeat.Start(
            token => notifications.SendStepProgressAsync(msg.JobId, msg.StepId, step.Progress, StepStatus.Running, token),
            TimeSpan.FromSeconds(15),
            logger,
            nameof(ExportVhdxConsumer),
            ct);

        try
        {
            // Verify VM state is Off for offline export
            var stateResult = await hyperVClient.GetVmStateAsync(msg.SourceConnectionId, msg.VmId, ct);
            if (!stateResult.IsSuccess)
            {
                await FailStepAsync(job, step, stateResult.ErrorMessage ?? "Failed to check VM state", msg, context, ct);
                return;
            }

            if (!string.Equals(stateResult.Value, "Off", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(stateResult.Value, "ShutDown", StringComparison.OrdinalIgnoreCase))
            {
                await FailStepAsync(job, step, $"VM {msg.VmId} is not offline (current state: {stateResult.Value}). Offline export requires VM to be powered off.", msg, context, ct);
                return;
            }

            var progress = new Progress<int>(percent =>
            {
                step.UpdateProgress(percent);
                _ = notifications.SendStepProgressAsync(msg.JobId, msg.StepId, percent, StepStatus.Running, ct);
            });

            var exportResult = await hyperVClient.ExportVhdxAsync(
                msg.SourceConnectionId, msg.VmId, msg.DiskKey, progress, ct);

            if (!exportResult.IsSuccess)
            {
                await FailStepAsync(job, step, exportResult.ErrorMessage ?? "VHDX Export failed", msg, context, ct);
                return;
            }

            await using var stream = exportResult.Value!;
            var storageKey = $"jobs/{msg.JobId}/export/{msg.DiskKey}.vhdx";
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
                new Dictionary<string, string> { ["ExportedStorageKey"] = storageKey }), ct);
        }
        catch (Exception ex)
        {
            LogFailed(logger, ex, msg.JobId, msg.StepId);
            await FailStepAsync(job, step, ex.Message, msg, context, ct);
        }
    }

    private async Task FailStepAsync(
        Domain.Aggregates.MigrationJob.MigrationJob job,
        Domain.Aggregates.MigrationJob.JobStep step,
        string error,
        ExportVhdxMessage msg,
        ConsumeContext<ExportVhdxMessage> context,
        CancellationToken ct)
    {
        if (step.RetryCount < step.MaxRetries)
        {
            step.Fail(error);
            step.Retry();
        }
        else
        {
            step.Fail(error);
        }

        job.UpdateProgress();
        await jobRepository.UpdateAsync(job, ct);
        await notifications.SendStepProgressAsync(msg.JobId, msg.StepId, step.Progress, step.Status, ct);
        await context.Publish(new StepFailedMessage(msg.JobId, msg.StepId, step.Name, error, msg.CorrelationId), ct);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ExportVhdx starting for Job {JobId}, Step {StepId}")]
    private static partial void LogStarting(ILogger logger, Guid jobId, Guid stepId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Job {JobId} not found")]
    private static partial void LogJobNotFound(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Step {StepId} not found in Job {JobId}")]
    private static partial void LogStepNotFound(ILogger logger, Guid stepId, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "ExportVhdx failed for Job {JobId}, Step {StepId}")]
    private static partial void LogFailed(ILogger logger, Exception ex, Guid jobId, Guid stepId);
}
