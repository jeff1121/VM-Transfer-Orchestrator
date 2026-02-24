using MassTransit;
using Microsoft.Extensions.Logging;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Enums;
using VMTO.Worker.Messages;

namespace VMTO.Worker.Consumers;

public sealed partial class ImportToPveConsumer(
    IJobRepository jobRepository,
    IPveClient pveClient,
    INotificationService notifications,
    ILogger<ImportToPveConsumer> logger) : IConsumer<ImportToPveMessage>
{
    public async Task Consume(ConsumeContext<ImportToPveMessage> context)
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
            var createResult = await pveClient.CreateVmAsync(
                msg.TargetConnectionId, msg.VmName, msg.Cores, msg.MemoryMb, ct);

            if (!createResult.IsSuccess)
            {
                await FailStepAsync(job, step, createResult.ErrorMessage ?? "VM creation failed", msg, context, ct);
                return;
            }

            var vmId = createResult.Value;
            step.UpdateProgress(30);
            await notifications.SendStepProgressAsync(msg.JobId, msg.StepId, 30, StepStatus.Running, ct);

            var progress = new Progress<int>(async percent =>
            {
                var scaled = 30 + (int)(percent * 0.7);
                step.UpdateProgress(scaled);
                await notifications.SendStepProgressAsync(msg.JobId, msg.StepId, scaled, StepStatus.Running, ct);
            });

            var importResult = await pveClient.ImportDiskAsync(
                msg.TargetConnectionId, vmId, msg.StorageUri, msg.DiskFormat, progress, ct);

            if (!importResult.IsSuccess)
            {
                await FailStepAsync(job, step, importResult.ErrorMessage ?? "Disk import failed", msg, context, ct);
                return;
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
    }

    private async Task FailStepAsync(
        Domain.Aggregates.MigrationJob.MigrationJob job,
        Domain.Aggregates.MigrationJob.JobStep step,
        string error,
        ImportToPveMessage msg,
        ConsumeContext<ImportToPveMessage> context,
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

    [LoggerMessage(Level = LogLevel.Information, Message = "ImportToPve starting for Job {JobId}, Step {StepId}")]
    private static partial void LogStarting(ILogger logger, Guid jobId, Guid stepId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Job {JobId} not found")]
    private static partial void LogJobNotFound(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Step {StepId} not found in Job {JobId}")]
    private static partial void LogStepNotFound(ILogger logger, Guid stepId, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "ImportToPve failed for Job {JobId}, Step {StepId}")]
    private static partial void LogFailed(ILogger logger, Exception ex, Guid jobId, Guid stepId);
}
