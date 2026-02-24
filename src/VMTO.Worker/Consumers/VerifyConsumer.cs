using MassTransit;
using Microsoft.Extensions.Logging;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Enums;
using VMTO.Infrastructure.Storage;
using VMTO.Worker.Messages;

namespace VMTO.Worker.Consumers;

public sealed partial class VerifyConsumer(
    IJobRepository jobRepository,
    IArtifactRepository artifactRepository,
    StorageAdapterFactory storageFactory,
    INotificationService notifications,
    ILogger<VerifyConsumer> logger) : IConsumer<VerifyMessage>
{
    public async Task Consume(ConsumeContext<VerifyMessage> context)
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
            var artifact = await artifactRepository.GetByIdAsync(msg.ArtifactId, ct);
            if (artifact is null)
            {
                await FailStepAsync(job, step, $"Artifact {msg.ArtifactId} not found", msg, context, ct);
                return;
            }

            var storage = storageFactory.Create(job.StorageTarget.Type);

            var checksumResult = await storage.GetChecksumAsync(artifact.StorageUri, ct);
            if (!checksumResult.IsSuccess)
            {
                await FailStepAsync(job, step, checksumResult.ErrorMessage ?? "Checksum retrieval failed", msg, context, ct);
                return;
            }

            step.UpdateProgress(50);
            await notifications.SendStepProgressAsync(msg.JobId, msg.StepId, 50, StepStatus.Running, ct);

            if (!string.Equals(checksumResult.Value, msg.ExpectedChecksum, StringComparison.OrdinalIgnoreCase))
            {
                var error = $"Checksum mismatch: expected {msg.ExpectedChecksum}, got {checksumResult.Value}";
                await FailStepAsync(job, step, error, msg, context, ct);
                return;
            }

            step.Complete();
            job.UpdateProgress();

            var allDone = job.Steps.All(s => s.Status is StepStatus.Succeeded or StepStatus.Skipped);
            if (allDone)
            {
                job.Complete();
                await notifications.SendJobProgressAsync(msg.JobId, 100, JobStatus.Succeeded, ct);
            }

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
        VerifyMessage msg,
        ConsumeContext<VerifyMessage> context,
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Verify starting for Job {JobId}, Step {StepId}")]
    private static partial void LogStarting(ILogger logger, Guid jobId, Guid stepId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Job {JobId} not found")]
    private static partial void LogJobNotFound(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Step {StepId} not found in Job {JobId}")]
    private static partial void LogStepNotFound(ILogger logger, Guid stepId, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Verify failed for Job {JobId}, Step {StepId}")]
    private static partial void LogFailed(ILogger logger, Exception ex, Guid jobId, Guid stepId);
}
