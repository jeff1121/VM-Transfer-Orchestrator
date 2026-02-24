using MassTransit;
using Microsoft.Extensions.Logging;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.Artifact;
using VMTO.Domain.Enums;
using VMTO.Infrastructure.Storage;
using VMTO.Worker.Messages;

namespace VMTO.Worker.Consumers;

public sealed partial class ConvertDiskConsumer(
    IJobRepository jobRepository,
    IQemuImgService qemuImg,
    StorageAdapterFactory storageFactory,
    INotificationService notifications,
    ILogger<ConvertDiskConsumer> logger) : IConsumer<ConvertDiskMessage>
{
    public async Task Consume(ConsumeContext<ConvertDiskMessage> context)
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

        step.Start();
        await jobRepository.UpdateAsync(job, ct);
        await notifications.SendStepProgressAsync(msg.JobId, msg.StepId, 0, StepStatus.Running, ct);

        var inputPath = Path.Combine(Path.GetTempPath(), $"vmto-{msg.JobId}-input");
        var outputPath = Path.Combine(Path.GetTempPath(), $"vmto-{msg.JobId}-output");

        try
        {
            var storage = storageFactory.Create(job.StorageTarget.Type);

            var downloadResult = await storage.DownloadAsync(msg.InputStorageKey, ct);
            if (!downloadResult.IsSuccess)
            {
                await FailStepAsync(job, step, downloadResult.ErrorMessage ?? "Download failed", msg, context, ct);
                return;
            }

            await using (var sourceStream = downloadResult.Value!)
            await using (var fileStream = File.Create(inputPath))
            {
                await sourceStream.CopyToAsync(fileStream, ct);
            }

            step.UpdateProgress(30);
            await notifications.SendStepProgressAsync(msg.JobId, msg.StepId, 30, StepStatus.Running, ct);

            if (!Enum.TryParse<ArtifactFormat>(msg.TargetFormat, ignoreCase: true, out var targetFormat))
            {
                await FailStepAsync(job, step, $"Unknown target format: {msg.TargetFormat}", msg, context, ct);
                return;
            }

            var progress = new Progress<int>(async percent =>
            {
                var scaled = 30 + (int)(percent * 0.5);
                step.UpdateProgress(scaled);
                await notifications.SendStepProgressAsync(msg.JobId, msg.StepId, scaled, StepStatus.Running, ct);
            });

            var convertResult = await qemuImg.ConvertAsync(inputPath, outputPath, targetFormat, progress, ct);
            if (!convertResult.IsSuccess)
            {
                await FailStepAsync(job, step, convertResult.ErrorMessage ?? "Conversion failed", msg, context, ct);
                return;
            }

            step.UpdateProgress(80);
            await notifications.SendStepProgressAsync(msg.JobId, msg.StepId, 80, StepStatus.Running, ct);

            await using (var outputStream = File.OpenRead(outputPath))
            {
                var uploadResult = await storage.UploadAsync(
                    msg.OutputStorageKey, outputStream, outputStream.Length, "application/octet-stream", ct);

                if (!uploadResult.IsSuccess)
                {
                    await FailStepAsync(job, step, uploadResult.ErrorMessage ?? "Upload failed", msg, context, ct);
                    return;
                }
            }

            step.Complete();
            job.UpdateProgress();
            await jobRepository.UpdateAsync(job, ct);
            await notifications.SendStepProgressAsync(msg.JobId, msg.StepId, 100, StepStatus.Succeeded, ct);

            await context.Publish(new StepCompletedMessage(msg.JobId, msg.StepId, step.Name, msg.CorrelationId), ct);
        }
        catch (Exception ex)
        {
            LogFailed(logger, ex, msg.JobId, msg.StepId);
            await FailStepAsync(job, step, ex.Message, msg, context, ct);
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(outputPath);
        }
    }

    private async Task FailStepAsync(
        Domain.Aggregates.MigrationJob.MigrationJob job,
        Domain.Aggregates.MigrationJob.JobStep step,
        string error,
        ConvertDiskMessage msg,
        ConsumeContext<ConvertDiskMessage> context,
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

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best effort cleanup */ }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ConvertDisk starting for Job {JobId}, Step {StepId}")]
    private static partial void LogStarting(ILogger logger, Guid jobId, Guid stepId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Job {JobId} not found")]
    private static partial void LogJobNotFound(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Step {StepId} not found in Job {JobId}")]
    private static partial void LogStepNotFound(ILogger logger, Guid stepId, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "ConvertDisk failed for Job {JobId}, Step {StepId}")]
    private static partial void LogFailed(ILogger logger, Exception ex, Guid jobId, Guid stepId);
}
