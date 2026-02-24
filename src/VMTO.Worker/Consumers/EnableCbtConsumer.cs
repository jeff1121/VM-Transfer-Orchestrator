using MassTransit;
using Microsoft.Extensions.Logging;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Enums;
using VMTO.Worker.Messages;

namespace VMTO.Worker.Consumers;

public sealed partial class EnableCbtConsumer(
    IJobRepository jobRepository,
    IVSphereClient vSphereClient,
    INotificationService notifications,
    ILogger<EnableCbtConsumer> logger) : IConsumer<EnableCbtMessage>
{
    public async Task Consume(ConsumeContext<EnableCbtMessage> context)
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
            // Check if CBT is already enabled
            var cbtCheck = await vSphereClient.IsCbtEnabledAsync(msg.SourceConnectionId, msg.VmId, ct);
            if (cbtCheck.IsSuccess && cbtCheck.Value)
            {
                LogCbtAlreadyEnabled(logger, msg.JobId, msg.VmId);
            }
            else
            {
                // TODO: Enable CBT via vSphere API
                // var enableResult = await vSphereClient.EnableCbtAsync(msg.SourceConnectionId, msg.VmId, ct);
                // if (!enableResult.IsSuccess) { await FailStepAsync(...); return; }
                // TODO: Create + delete snapshot to initialize CBT tracking
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
        EnableCbtMessage msg,
        ConsumeContext<EnableCbtMessage> context,
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

    [LoggerMessage(Level = LogLevel.Information, Message = "EnableCbt starting for Job {JobId}, Step {StepId}")]
    private static partial void LogStarting(ILogger logger, Guid jobId, Guid stepId);

    [LoggerMessage(Level = LogLevel.Information, Message = "CBT already enabled for Job {JobId}, VM {VmId}")]
    private static partial void LogCbtAlreadyEnabled(ILogger logger, Guid jobId, string vmId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Job {JobId} not found")]
    private static partial void LogJobNotFound(ILogger logger, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Step {StepId} not found in Job {JobId}")]
    private static partial void LogStepNotFound(ILogger logger, Guid stepId, Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "EnableCbt failed for Job {JobId}, Step {StepId}")]
    private static partial void LogFailed(ILogger logger, Exception ex, Guid jobId, Guid stepId);
}
