namespace VMTO.Infrastructure.Persistence.Entities;

/// <summary>Webhook 訂閱設定</summary>
public sealed class WebhookSubscription
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Webhook 類型：Slack, Teams, Email, Http</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>目標 URL 或 Email 地址</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>觸發事件類型（逗號分隔）：JobSucceeded, JobFailed, StepFailed</summary>
    public string Events { get; set; } = string.Empty;

    /// <summary>是否啟用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>自訂 HTTP headers（JSON 格式，僅 Http 類型使用）</summary>
    public string? CustomHeaders { get; set; }

    /// <summary>密鑰（用於 HMAC 簽章驗證）</summary>
    public string? Secret { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
