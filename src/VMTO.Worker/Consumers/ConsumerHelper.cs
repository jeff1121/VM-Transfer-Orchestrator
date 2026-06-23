using MassTransit;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Worker.Messages;

namespace VMTO.Worker.Consumers;

internal static class ConsumerHelper
{
    public static async Task FailStepAsync(
        MigrationJob job,
        JobStep step,
        string error,
        Guid jobId,
        Guid stepId,
        string stepName,
        Guid correlationId,
        IPublishEndpoint publisher,
        IJobRepository jobRepository,
        INotificationService notifications,
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
        await notifications.SendStepProgressAsync(jobId, stepId, step.Progress, step.Status, ct);
        await publisher.Publish(new StepFailedMessage(jobId, stepId, stepName, error, correlationId), ct);
    }
}
