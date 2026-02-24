using MassTransit;
using Microsoft.Extensions.Logging;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.Artifact;
using VMTO.Domain.Enums;
using VMTO.Domain.ValueObjects;
using VMTO.Infrastructure.Storage;
using VMTO.Worker.Messages;

namespace VMTO.Worker.Consumers;

public sealed partial class UploadArtifactConsumer(
    IJobRepository jobRepository,
    IArtifactRepository artifactRepository,
    StorageAdapterFactory storageFactory,
    INotificationService notifications,
    ILogger<UploadArtifactConsumer> logger) : IConsumer<UploadArtifactMessage>
{
    public async Task Consume(ConsumeContext<UploadArtifactMessage> context)
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

        try
        {
            var storage = storageFactory.Create(job.StorageTarget.Type);

            await using var fileStream = File.OpenRead(msg.LocalPath);
            var uploadResult = await storage.UploadAsync(
                msg.StorageKey, fileStream, fileStream.Length, "application/octet-stream", ct);

            if (!uploadResult.IsSuccess)
            {
                await FailStepAsync(job, step, uploadResult.ErrorMessage ?? "Upload failed", msg, context, ct);
                return;
            }

            step.UpdateProgress(70);
            await notifications.SendStepProgressAsync(msg.JobId, msg.StepId, 70, StepStatus.Running, ct);

            var checksumResult = await storage.GetChecksumAsync(msg.StorageKey, ct);
            var checksumValue = checksumResult.IsSuccess ? checksumResult.Value! : "unavailable";
            var checksum = new Checksum("SHA256", checksumValue);

            var fileInfo = new FileInfo(msg.LocalPath);
            var format = Path.GetExtension(msg.LocalPath).ToLowerInvariant() switch
            {
                ".qcow2" => ArtifactFormat.Qcow2,
                ".raw" => ArtifactFormat.Raw,
                _ => ArtifactFormat.Vmdk,
            };

            var artifact = new Artifact(
                msg.JobId,
                Path.GetFileName(msg.LocalPath),
                format,
                checksum,
                fileInfo.Length,
                msg.StorageKey);

            await artifactRepository.AddAsync(artifact, ct);

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
    }

    private async Task FailStepAsync(
        Domain.Aggregates.MigrationJob.MigrationJob job,
        Domain.Aggregates.MigrationJob.JobStep step,
        string error,
        UploadArtifactMessage msg,
        ConsumeContext<UploadArtifactMessage> context,
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

    [LoggerMessage(Level = LogLevel.Information, Message = "UploadArtifact starting for Job {JobId}, Step {StepId}")]
    private static partial void LogStarting(ILogger logger, Guid jobId, Guid stepId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Job {JobId} not found")]
    private static partial void LogJobNotFound(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Step {StepId} not found in Job {JobId}")]
    private static partial void LogStepNotFound(ILogger logger, Guid stepId, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "UploadArtifact failed for Job {JobId}, Step {StepId}")]
    private static partial void LogFailed(ILogger logger, Exception ex, Guid jobId, Guid stepId);
}
