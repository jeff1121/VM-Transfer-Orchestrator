using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Logging;
using VMTO.Application.Ports.Services;
using VMTO.Infrastructure.Persistence;
using VMTO.Infrastructure.Persistence.Entities;
using VMTO.Worker.Messages;

namespace VMTO.Worker.Consumers;

public sealed partial class DlqConsumer(
    AppDbContext db,
    IWebhookService webhookService,
    ILogger<DlqConsumer> logger) : IConsumer<StepFailedMessage>
{
    public async Task Consume(ConsumeContext<StepFailedMessage> context)
    {
        var message = context.Message;
        var queueName = context.ReceiveContext.InputAddress?.AbsolutePath.Trim('/') ?? "unknown";

        var entry = new DeadLetterLogEntry
        {
            Id = Guid.NewGuid(),
            MessageType = nameof(StepFailedMessage),
            QueueName = queueName,
            Payload = JsonSerializer.Serialize(message),
            Error = message.Error,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        db.DeadLetterLogs.Add(entry);
        await db.SaveChangesAsync(context.CancellationToken);

        LogCaptured(logger, entry.Id, message.JobId, message.StepId, queueName);

        await webhookService.NotifyStepFailedAsync(
            message.JobId,
            message.StepId,
            $"DLQ:{message.StepName}",
            message.Error,
            context.CancellationToken);
    }

    [LoggerMessage(
        EventId = 9301,
        Level = LogLevel.Warning,
        Message = "DLQ captured message {EntryId} for Job {JobId}, Step {StepId}, Queue {QueueName}")]
    private static partial void LogCaptured(ILogger logger, Guid entryId, Guid jobId, Guid stepId, string queueName);
}
