using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using VMTO.API.Auth;
using VMTO.Domain.Aggregates.Connection;
using VMTO.Domain.ValueObjects;
using VMTO.Infrastructure.Jobs;
using VMTO.Infrastructure.Ops;
using VMTO.Infrastructure.Persistence;
using VMTO.Infrastructure.Persistence.Entities;
using VMTO.Infrastructure.Resilience;
using VMTO.Worker.Messages;

namespace VMTO.API.Endpoints;

public sealed record DlqLogDto(
    Guid Id,
    string MessageType,
    string QueueName,
    string? Error,
    string Status,
    DateTime CreatedAt,
    DateTime? ReplayedAt);

public sealed record DlqListResponse(
    DlqLogDto[] Items,
    int Total,
    int Page,
    int PageSize);

public sealed record ChaosControlRequest(
    bool Enabled,
    double? FailureRate,
    int? MaxDelayMs,
    double? TimeoutRate);

public sealed record ConnectionBackupDto(
    string Name,
    string Type,
    string Endpoint,
    string CipherText,
    string? KeyId);

public sealed record WebhookBackupDto(
    string Name,
    string Type,
    string Target,
    string Events,
    bool IsEnabled,
    string? CustomHeaders,
    string? Secret);

public sealed record OpsConfigBackupDto(
    DateTime ExportedAt,
    object Chaos,
    ConnectionBackupDto[] Connections,
    WebhookBackupDto[] Webhooks);

public sealed record OpsConfigRestoreRequest(
    bool ReplaceExisting,
    ChaosControlRequest? Chaos,
    ConnectionBackupDto[] Connections,
    WebhookBackupDto[] Webhooks);

public static class OpsEndpoints
{
    public static void MapOpsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ops")
            .WithTags("Ops")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        group.MapGet("/dlq", ListDlq);
        group.MapPost("/dlq/{id:guid}/replay", ReplayDlq);
        group.MapGet("/chaos", GetChaos);
        group.MapPost("/chaos", ConfigureChaos);

        group.MapGet("/health-report", GetHealthReport);
        group.MapGet("/stuck-jobs", ListStuckJobs);
        group.MapPost("/stuck-jobs/{id:guid}/heal", HealStuckJob);
        group.MapGet("/storage-usage", GetStorageUsage);
        group.MapGet("/system-info", GetSystemInfo);

        group.MapPost("/backup/config", BackupConfig);
        group.MapPost("/restore/config", RestoreConfig);
    }

    private static async Task<IResult> ListDlq(
        AppDbContext db,
        int page = 1,
        int pageSize = 20,
        string? status = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = db.DeadLetterLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new DlqLogDto(
                x.Id,
                x.MessageType,
                x.QueueName,
                x.Error,
                x.Status,
                x.CreatedAt,
                x.ReplayedAt))
            .ToArrayAsync(ct);

        return Results.Ok(new DlqListResponse(items, total, page, pageSize));
    }

    private static async Task<IResult> ReplayDlq(
        Guid id,
        AppDbContext db,
        IPublishEndpoint publishEndpoint,
        CancellationToken ct = default)
    {
        var entry = await db.DeadLetterLogs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entry is null)
        {
            return Results.NotFound(new { code = "DLQ_NOT_FOUND", message = $"找不到 DLQ 記錄 {id}" });
        }

        if (entry.Status.Equals("replayed", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { code = "DLQ_ALREADY_REPLAYED", message = "此訊息已重播。" });
        }

        if (!entry.MessageType.Equals(nameof(StepFailedMessage), StringComparison.Ordinal))
        {
            return Results.BadRequest(new { code = "DLQ_UNSUPPORTED_TYPE", message = $"不支援重播類型：{entry.MessageType}" });
        }

        StepFailedMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<StepFailedMessage>(entry.Payload);
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(new { code = "DLQ_PAYLOAD_INVALID", message = ex.Message });
        }

        if (message is null)
        {
            return Results.BadRequest(new { code = "DLQ_PAYLOAD_INVALID", message = "無法解析 DLQ payload。" });
        }

        await publishEndpoint.Publish(message, ct);
        entry.Status = "replayed";
        entry.ReplayedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { id = entry.Id, status = entry.Status, replayedAt = entry.ReplayedAt });
    }

    private static IResult GetChaos(IChaosPolicy chaosPolicy)
    {
        var snapshot = chaosPolicy.GetSnapshot();
        return Results.Ok(snapshot);
    }

    private static IResult ConfigureChaos(
        ChaosControlRequest request,
        IChaosPolicy chaosPolicy)
    {
        chaosPolicy.Configure(
            request.Enabled,
            request.FailureRate,
            request.MaxDelayMs,
            request.TimeoutRate);

        var snapshot = chaosPolicy.GetSnapshot();
        return Results.Ok(snapshot);
    }

    private static async Task<IResult> GetHealthReport(
        IOpsSnapshotStore snapshotStore,
        HealthReportJob healthReportJob,
        CancellationToken ct)
    {
        var latest = snapshotStore.GetLatestHealthSnapshot() ?? await healthReportJob.RunAsync(ct);
        return Results.Ok(new
        {
            latest,
            history = snapshotStore.GetHealthSnapshots(20)
        });
    }

    private static async Task<IResult> ListStuckJobs(
        SelfHealingService selfHealingService,
        CancellationToken ct)
    {
        var items = await selfHealingService.ListStuckJobsAsync(ct);
        return Results.Ok(new { items, total = items.Count });
    }

    private static async Task<IResult> HealStuckJob(
        Guid id,
        SelfHealingService selfHealingService,
        CancellationToken ct)
    {
        var healed = await selfHealingService.HealStuckJobAsync(id, ct);
        return healed
            ? Results.Ok(new { healed = true, jobId = id })
            : Results.NotFound(new { healed = false, message = "找不到可修復的卡住任務。" });
    }

    private static async Task<IResult> GetStorageUsage(
        IOpsSnapshotStore snapshotStore,
        StorageUsageJob storageUsageJob,
        CancellationToken ct)
    {
        var latest = snapshotStore.GetLatestStorageSnapshot() ?? await storageUsageJob.RunAsync(ct);
        return Results.Ok(new
        {
            latest,
            history = snapshotStore.GetStorageSnapshots(20)
        });
    }

    private static IResult GetSystemInfo()
    {
        var process = Environment.ProcessId;
        var startedAt = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        return Results.Ok(new
        {
            version,
            runtime = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription,
            processId = process,
            machineName = Environment.MachineName,
            startedAt,
            uptimeSeconds = (long)(DateTime.UtcNow - startedAt).TotalSeconds
        });
    }

    private static async Task<IResult> BackupConfig(
        AppDbContext db,
        IChaosPolicy chaosPolicy,
        CancellationToken ct)
    {
        var connections = await db.Connections
            .Select(c => new ConnectionBackupDto(
                c.Name,
                c.Type.ToString(),
                c.Endpoint,
                c.EncryptedSecret.CipherText,
                c.EncryptedSecret.KeyId))
            .ToArrayAsync(ct);

        var webhooks = await db.WebhookSubscriptions
            .Select(w => new WebhookBackupDto(
                w.Name,
                w.Type,
                w.Target,
                w.Events,
                w.IsEnabled,
                w.CustomHeaders,
                w.Secret))
            .ToArrayAsync(ct);

        var payload = new OpsConfigBackupDto(
            DateTime.UtcNow,
            chaosPolicy.GetSnapshot(),
            connections,
            webhooks);

        return Results.Ok(payload);
    }

    private static async Task<IResult> RestoreConfig(
        OpsConfigRestoreRequest request,
        AppDbContext db,
        IChaosPolicy chaosPolicy,
        CancellationToken ct)
    {
        if (request.ReplaceExisting)
        {
            db.WebhookSubscriptions.RemoveRange(db.WebhookSubscriptions);
            db.Connections.RemoveRange(db.Connections);
            await db.SaveChangesAsync(ct);
        }

        var newConnections = new List<Connection>();
        foreach (var dto in request.Connections)
        {
            if (!Enum.TryParse<ConnectionType>(dto.Type, true, out var connectionType))
            {
                return Results.BadRequest(new { code = "INVALID_CONNECTION_TYPE", message = $"不支援的連線類型：{dto.Type}" });
            }

            var connection = new Connection(
                dto.Name,
                connectionType,
                dto.Endpoint,
                new EncryptedSecret(dto.CipherText, dto.KeyId));
            newConnections.Add(connection);
        }

        var newWebhooks = request.Webhooks.Select(dto => new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Type = dto.Type,
            Target = dto.Target,
            Events = dto.Events,
            IsEnabled = dto.IsEnabled,
            CustomHeaders = dto.CustomHeaders,
            Secret = dto.Secret,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToArray();

        await db.Connections.AddRangeAsync(newConnections, ct);
        await db.WebhookSubscriptions.AddRangeAsync(newWebhooks, ct);
        await db.SaveChangesAsync(ct);

        if (request.Chaos is not null)
        {
            chaosPolicy.Configure(
                request.Chaos.Enabled,
                request.Chaos.FailureRate,
                request.Chaos.MaxDelayMs,
                request.Chaos.TimeoutRate);
        }

        return Results.Ok(new
        {
            restoredConnections = newConnections.Count,
            restoredWebhooks = newWebhooks.Length,
            replaceExisting = request.ReplaceExisting
        });
    }
}
