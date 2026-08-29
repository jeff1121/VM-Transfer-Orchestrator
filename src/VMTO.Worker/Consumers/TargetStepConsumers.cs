using System.Globalization;
using MassTransit;
using Microsoft.Extensions.Logging;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Enums;
using VMTO.Worker.Messages;
using VMTO.Worker.Telemetry;

namespace VMTO.Worker.Consumers;

public sealed partial class ProvisionTargetVmConsumer(
    IJobRepository jobRepository,
    IConnectionRepository connectionRepository,
    ITargetPlatformProviderFactory targetFactory,
    INotificationService notifications,
    ILogger<ProvisionTargetVmConsumer> logger) : IConsumer<ProvisionTargetVmMessage>
{
    public async Task Consume(ConsumeContext<ProvisionTargetVmMessage> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;
        var job = await jobRepository.GetByIdAsync(msg.JobId, ct);
        var step = job?.Steps.FirstOrDefault(s => s.Id == msg.StepId);
        if (job is null || step is null) return;

        using var telemetry = WorkerTracing.StartStepActivity(nameof(ProvisionTargetVmConsumer), step, msg.JobId, msg.StepId, msg.CorrelationId);
        step.Start();
        await jobRepository.UpdateAsync(job, ct);
        await notifications.SendStepProgressAsync(msg.JobId, msg.StepId, 0, StepStatus.Running, ct);

        try
        {
            var connection = await connectionRepository.GetByIdAsync(msg.TargetConnectionId, ct);
            if (connection is null)
            {
                await ConsumerHelper.FailStepAsync(job, step, "Target connection not found", msg.JobId, msg.StepId, step.Name, msg.CorrelationId, context, jobRepository, notifications, ct);
                return;
            }

            var target = targetFactory.GetProvider(connection.Type);
            var created = await target.CreateVmAsync(msg.TargetConnectionId, msg.VmName, msg.Cores, msg.MemoryMb, ct);
            if (!created.IsSuccess)
            {
                await ConsumerHelper.FailStepAsync(job, step, created.ErrorMessage ?? "VM creation failed", msg.JobId, msg.StepId, step.Name, msg.CorrelationId, context, jobRepository, notifications, ct);
                return;
            }

            step.Complete();
            job.UpdateProgress();
            await jobRepository.UpdateAsync(job, ct);
            await notifications.SendStepProgressAsync(msg.JobId, msg.StepId, 100, StepStatus.Succeeded, ct);
            await context.Publish(new StepCompletedMessage(msg.JobId, msg.StepId, step.Name, msg.CorrelationId,
                new Dictionary<string, string> { ["TargetVmId"] = created.Value.ToString(CultureInfo.InvariantCulture) }), ct);
        }
        catch (Exception ex)
        {
            LogFailed(logger, ex, msg.JobId, msg.StepId);
            await ConsumerHelper.FailStepAsync(job, step, ex.Message, msg.JobId, msg.StepId, step.Name, msg.CorrelationId, context, jobRepository, notifications, ct);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "ProvisionTargetVm failed for Job {JobId}, Step {StepId}")]
    private static partial void LogFailed(ILogger logger, Exception ex, Guid jobId, Guid stepId);
}

public sealed partial class AttachDiskConsumer(
    IJobRepository jobRepository,
    IConnectionRepository connectionRepository,
    ITargetPlatformProviderFactory targetFactory,
    INotificationService notifications,
    ILogger<AttachDiskConsumer> logger) : IConsumer<AttachDiskMessage>
{
    public async Task Consume(ConsumeContext<AttachDiskMessage> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;
        var job = await jobRepository.GetByIdAsync(msg.JobId, ct);
        var step = job?.Steps.FirstOrDefault(s => s.Id == msg.StepId);
        if (job is null || step is null) return;

        using var telemetry = WorkerTracing.StartStepActivity(nameof(AttachDiskConsumer), step, msg.JobId, msg.StepId, msg.CorrelationId);
        step.Start();
        await jobRepository.UpdateAsync(job, ct);

        try
        {
            var connection = await connectionRepository.GetByIdAsync(msg.TargetConnectionId, ct);
            if (connection is null)
            {
                await ConsumerHelper.FailStepAsync(job, step, "Target connection not found", msg.JobId, msg.StepId, step.Name, msg.CorrelationId, context, jobRepository, notifications, ct);
                return;
            }

            if (!int.TryParse(msg.TargetVmId, out var vmId))
            {
                await ConsumerHelper.FailStepAsync(job, step, "Target VM id is missing", msg.JobId, msg.StepId, step.Name, msg.CorrelationId, context, jobRepository, notifications, ct);
                return;
            }

            var target = targetFactory.GetProvider(connection.Type);
            var imported = await target.ImportDiskAsync(msg.TargetConnectionId, vmId, msg.StorageUri, msg.DiskFormat, null, ct);
            if (!imported.IsSuccess)
            {
                await ConsumerHelper.FailStepAsync(job, step, imported.ErrorMessage ?? "Disk attach failed", msg.JobId, msg.StepId, step.Name, msg.CorrelationId, context, jobRepository, notifications, ct);
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
            await ConsumerHelper.FailStepAsync(job, step, ex.Message, msg.JobId, msg.StepId, step.Name, msg.CorrelationId, context, jobRepository, notifications, ct);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "AttachDisk failed for Job {JobId}, Step {StepId}")]
    private static partial void LogFailed(ILogger logger, Exception ex, Guid jobId, Guid stepId);
}

public sealed partial class ConfigureTargetVmConsumer(
    IJobRepository jobRepository,
    IConnectionRepository connectionRepository,
    ITargetPlatformProviderFactory targetFactory,
    INotificationService notifications,
    ILogger<ConfigureTargetVmConsumer> logger) : IConsumer<ConfigureTargetVmMessage>
{
    public async Task Consume(ConsumeContext<ConfigureTargetVmMessage> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;
        var job = await jobRepository.GetByIdAsync(msg.JobId, ct);
        var step = job?.Steps.FirstOrDefault(s => s.Id == msg.StepId);
        if (job is null || step is null) return;

        using var telemetry = WorkerTracing.StartStepActivity(nameof(ConfigureTargetVmConsumer), step, msg.JobId, msg.StepId, msg.CorrelationId);
        step.Start();
        await jobRepository.UpdateAsync(job, ct);

        try
        {
            var connection = await connectionRepository.GetByIdAsync(msg.TargetConnectionId, ct);
            if (connection is null)
            {
                await ConsumerHelper.FailStepAsync(job, step, "Target connection not found", msg.JobId, msg.StepId, step.Name, msg.CorrelationId, context, jobRepository, notifications, ct);
                return;
            }

            if (!int.TryParse(msg.TargetVmId, out var vmId))
            {
                await ConsumerHelper.FailStepAsync(job, step, "Target VM id is missing", msg.JobId, msg.StepId, step.Name, msg.CorrelationId, context, jobRepository, notifications, ct);
                return;
            }

            var target = targetFactory.GetProvider(connection.Type);
            var configured = await target.ConfigureVmAsync(msg.TargetConnectionId, vmId, [], ct);
            if (!configured.IsSuccess)
            {
                await ConsumerHelper.FailStepAsync(job, step, configured.ErrorMessage ?? "Configure failed", msg.JobId, msg.StepId, step.Name, msg.CorrelationId, context, jobRepository, notifications, ct);
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
            await ConsumerHelper.FailStepAsync(job, step, ex.Message, msg.JobId, msg.StepId, step.Name, msg.CorrelationId, context, jobRepository, notifications, ct);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "ConfigureTargetVm failed for Job {JobId}, Step {StepId}")]
    private static partial void LogFailed(ILogger logger, Exception ex, Guid jobId, Guid stepId);
}

public sealed partial class CleanupExportConsumer(
    IJobRepository jobRepository,
    IConnectionRepository connectionRepository,
    ISourcePlatformProviderFactory sourceFactory,
    INotificationService notifications,
    ILogger<CleanupExportConsumer> logger) : IConsumer<CleanupMessage>
{
    public async Task Consume(ConsumeContext<CleanupMessage> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;
        var job = await jobRepository.GetByIdAsync(msg.JobId, ct);
        var step = job?.Steps.FirstOrDefault(s => s.Id == msg.StepId);
        if (job is null || step is null) return;

        using var telemetry = WorkerTracing.StartStepActivity(nameof(CleanupExportConsumer), step, msg.JobId, msg.StepId, msg.CorrelationId);
        step.Start();
        await jobRepository.UpdateAsync(job, ct);

        try
        {
            var connection = await connectionRepository.GetByIdAsync(msg.SourceConnectionId, ct);
            if (connection is not null)
            {
                var source = sourceFactory.GetProvider(connection.Type);
                await source.CleanupExportAsync(msg.SourceConnectionId, msg.VmId, ct);
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
            await ConsumerHelper.FailStepAsync(job, step, ex.Message, msg.JobId, msg.StepId, step.Name, msg.CorrelationId, context, jobRepository, notifications, ct);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Cleanup failed for Job {JobId}, Step {StepId}")]
    private static partial void LogFailed(ILogger logger, Exception ex, Guid jobId, Guid stepId);
}

public sealed partial class TargetRollbackConsumer(
    IConnectionRepository connectionRepository,
    ITargetPlatformProviderFactory targetFactory,
    ILogger<TargetRollbackConsumer> logger) : IConsumer<TargetRollbackMessage>
{
    public async Task Consume(ConsumeContext<TargetRollbackMessage> context)
    {
        var msg = context.Message;
        try
        {
            var connection = await connectionRepository.GetByIdAsync(msg.TargetConnectionId, context.CancellationToken);
            if (connection is null) return;
            var target = targetFactory.GetProvider(connection.Type);
            await target.RollbackAsync(msg.TargetConnectionId, msg.TargetVmId, msg.JobId.ToString("N"), context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogFailed(logger, ex, msg.JobId);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Target rollback failed for Job {JobId}")]
    private static partial void LogFailed(ILogger logger, Exception ex, Guid jobId);
}
