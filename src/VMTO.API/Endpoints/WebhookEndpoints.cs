using Microsoft.EntityFrameworkCore;
using VMTO.API.Auth;
using VMTO.Application.Ports.Services;
using VMTO.Infrastructure.Persistence;
using VMTO.Infrastructure.Persistence.Entities;

namespace VMTO.API.Endpoints;

/// <summary>Webhook 訂閱管理 API 端點</summary>
public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/webhooks").WithTags("Webhooks").RequireAuthorization();

        // 所有 Webhook 端點僅限 Admin
        group.MapGet("/", ListWebhooks)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));
        group.MapGet("/{id:guid}", GetWebhook)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));
        group.MapPost("/", CreateWebhook)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
            .RequireRateLimiting("write");
        group.MapPut("/{id:guid}", UpdateWebhook)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
            .RequireRateLimiting("write");
        group.MapDelete("/{id:guid}", DeleteWebhook)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
            .RequireRateLimiting("write");
        group.MapPost("/{id:guid}/test", TestWebhook)
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
            .RequireRateLimiting("write");
    }

    private static async Task<IResult> ListWebhooks(AppDbContext db, CancellationToken ct)
    {
        var list = await db.WebhookSubscriptions
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

        return Results.Ok(list.Select(MapToDto));
    }

    private static async Task<IResult> GetWebhook(Guid id, AppDbContext db, CancellationToken ct)
    {
        var sub = await db.WebhookSubscriptions.FindAsync([id], ct);
        if (sub is null) return Results.NotFound();
        return Results.Ok(MapToDto(sub));
    }

    private static async Task<IResult> CreateWebhook(CreateWebhookRequest request, AppDbContext db, CancellationToken ct)
    {
        var sub = new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            Target = request.Target,
            Events = request.Events,
            CustomHeaders = request.CustomHeaders,
            Secret = request.Secret,
            CreatedAt = DateTime.UtcNow
        };

        db.WebhookSubscriptions.Add(sub);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/webhooks/{sub.Id}", MapToDto(sub));
    }

    private static async Task<IResult> UpdateWebhook(Guid id, UpdateWebhookRequest request, AppDbContext db, CancellationToken ct)
    {
        var sub = await db.WebhookSubscriptions.FindAsync([id], ct);
        if (sub is null) return Results.NotFound();

        sub.Name = request.Name;
        sub.Target = request.Target;
        sub.Events = request.Events;
        sub.IsEnabled = request.IsEnabled;
        sub.CustomHeaders = request.CustomHeaders;
        sub.Secret = request.Secret;
        sub.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Results.Ok(MapToDto(sub));
    }

    private static async Task<IResult> DeleteWebhook(Guid id, AppDbContext db, CancellationToken ct)
    {
        var sub = await db.WebhookSubscriptions.FindAsync([id], ct);
        if (sub is null) return Results.NotFound();

        db.WebhookSubscriptions.Remove(sub);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> TestWebhook(Guid id, AppDbContext db, IWebhookService webhookService, CancellationToken ct)
    {
        var sub = await db.WebhookSubscriptions.FindAsync([id], ct);
        if (sub is null) return Results.NotFound();

        // 發送測試通知（模擬 JobSucceeded 事件）
        await webhookService.NotifyJobCompletedAsync(Guid.Empty, "Succeeded", null, ct);
        return Results.Ok(new { message = "測試通知已發送" });
    }

    /// <summary>轉換為 DTO（不包含 Secret）</summary>
    private static WebhookDto MapToDto(WebhookSubscription sub) =>
        new(sub.Id, sub.Name, sub.Type, sub.Target, sub.Events, sub.IsEnabled, sub.CreatedAt, sub.UpdatedAt);
}

// --- 請求 / 回應 DTO ---

/// <summary>建立 Webhook 訂閱請求</summary>
public sealed record CreateWebhookRequest(
    string Name,
    string Type,
    string Target,
    string Events,
    string? CustomHeaders,
    string? Secret);

/// <summary>更新 Webhook 訂閱請求</summary>
public sealed record UpdateWebhookRequest(
    string Name,
    string Target,
    string Events,
    bool IsEnabled,
    string? CustomHeaders,
    string? Secret);

/// <summary>Webhook 訂閱回應（不包含 Secret）</summary>
public sealed record WebhookDto(
    Guid Id,
    string Name,
    string Type,
    string Target,
    string Events,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
