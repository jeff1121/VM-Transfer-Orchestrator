using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VMTO.Application.Ports.Services;
using VMTO.Infrastructure.Persistence;

namespace VMTO.Infrastructure.Notifications;

/// <summary>Webhook 通知服務實作：支援 Http、Slack、Teams、Email 類型</summary>
public sealed partial class WebhookService : IWebhookService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(AppDbContext db, IHttpClientFactory httpClientFactory, ILogger<WebhookService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // --- LoggerMessage 高效能日誌委派 ---

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email 類型 Webhook 尚未實作，訂閱 {SubscriptionId} 將被略過")]
    private partial void LogEmailNotImplemented(Guid subscriptionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "不支援的 Webhook 類型 {WebhookType}，訂閱 {SubscriptionId}")]
    private partial void LogUnsupportedType(string webhookType, Guid subscriptionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "發送 Webhook 通知失敗，訂閱 {SubscriptionId}，事件 {EventType}")]
    private partial void LogSendFailed(Exception ex, Guid subscriptionId, string eventType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "HTTP Webhook 回應非成功狀態 {StatusCode}，訂閱 {SubscriptionId}")]
    private partial void LogHttpNonSuccess(int statusCode, Guid subscriptionId);

    /// <inheritdoc />
    public async Task NotifyJobCompletedAsync(Guid jobId, string status, string? errorMessage, CancellationToken ct)
    {
        // 依據狀態決定事件類型
        var eventType = string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase)
            ? "JobSucceeded"
            : "JobFailed";

        var subscriptions = await GetMatchingSubscriptionsAsync(eventType, ct);

        var payload = new
        {
            @event = eventType,
            jobId,
            status,
            errorMessage,
            timestamp = DateTime.UtcNow
        };

        foreach (var sub in subscriptions)
        {
            await SendNotificationAsync(sub, eventType, payload, ct);
        }
    }

    /// <inheritdoc />
    public async Task NotifyStepFailedAsync(Guid jobId, Guid stepId, string stepName, string errorMessage, CancellationToken ct)
    {
        const string eventType = "StepFailed";

        var subscriptions = await GetMatchingSubscriptionsAsync(eventType, ct);

        var payload = new
        {
            @event = eventType,
            jobId,
            stepId,
            stepName,
            errorMessage,
            timestamp = DateTime.UtcNow
        };

        foreach (var sub in subscriptions)
        {
            await SendNotificationAsync(sub, eventType, payload, ct);
        }
    }

    /// <summary>查詢啟用且包含指定事件類型的訂閱</summary>
    private async Task<List<Persistence.Entities.WebhookSubscription>> GetMatchingSubscriptionsAsync(string eventType, CancellationToken ct)
    {
        var allEnabled = await _db.WebhookSubscriptions
            .Where(w => w.IsEnabled)
            .ToListAsync(ct);

        // 以逗號分隔比對事件類型
        return allEnabled
            .Where(w => w.Events.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(eventType, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>依訂閱類型發送通知</summary>
    private async Task SendNotificationAsync(Persistence.Entities.WebhookSubscription sub, string eventType, object payload, CancellationToken ct)
    {
        try
        {
            switch (sub.Type.ToUpperInvariant())
            {
                case "HTTP":
                    await SendHttpAsync(sub, payload, ct);
                    break;
                case "SLACK":
                    await SendSlackAsync(sub, eventType, payload, ct);
                    break;
                case "TEAMS":
                    await SendTeamsAsync(sub, eventType, payload, ct);
                    break;
                case "EMAIL":
                    LogEmailNotImplemented(sub.Id);
                    break;
                default:
                    LogUnsupportedType(sub.Type, sub.Id);
                    break;
            }
        }
        catch (Exception ex)
        {
            LogSendFailed(ex, sub.Id, eventType);
        }
    }

    /// <summary>HTTP POST JSON 到目標 URL，支援自訂 headers 與 HMAC 簽章</summary>
    private async Task SendHttpAsync(Persistence.Entities.WebhookSubscription sub, object payload, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("Webhook");
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, sub.Target) { Content = content };

        // 附加自訂 headers
        if (!string.IsNullOrWhiteSpace(sub.CustomHeaders))
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(sub.CustomHeaders);
            if (headers is not null)
            {
                foreach (var (key, value) in headers)
                {
                    request.Headers.TryAddWithoutValidation(key, value);
                }
            }
        }

        // HMAC-SHA256 簽章
        if (!string.IsNullOrWhiteSpace(sub.Secret))
        {
            var signature = ComputeHmacSha256(json, sub.Secret);
            request.Headers.TryAddWithoutValidation("X-Webhook-Signature", signature);
        }

        var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            LogHttpNonSuccess((int)response.StatusCode, sub.Id);
        }
    }

    /// <summary>發送 Slack Incoming Webhook 訊息</summary>
    private async Task SendSlackAsync(Persistence.Entities.WebhookSubscription sub, string eventType, object payload, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("Webhook");
        var json = JsonSerializer.Serialize(payload);
        var text = $"[VMTO] {eventType}: {json}";
        var slackPayload = new { text };
        await client.PostAsJsonAsync(sub.Target, slackPayload, ct);
    }

    /// <summary>發送 Microsoft Teams Adaptive Card 訊息</summary>
    private async Task SendTeamsAsync(Persistence.Entities.WebhookSubscription sub, string eventType, object payload, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("Webhook");
        var json = JsonSerializer.Serialize(payload);

        // Teams Incoming Webhook 使用 MessageCard 格式
        var teamsPayload = new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = new object[]
                        {
                            new { type = "TextBlock", text = $"VMTO — {eventType}", weight = "Bolder", size = "Medium" },
                            new { type = "TextBlock", text = json, wrap = true }
                        }
                    }
                }
            }
        };

        await client.PostAsJsonAsync(sub.Target, teamsPayload, ct);
    }

    /// <summary>計算 HMAC-SHA256 簽章</summary>
    private static string ComputeHmacSha256(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return $"sha256={Convert.ToHexStringLower(hash)}";
    }
}
