namespace VMTO.Application.Ports.Services;

/// <summary>Webhook 通知服務介面</summary>
public interface IWebhookService
{
    /// <summary>通知工作完成（成功或失敗）</summary>
    Task NotifyJobCompletedAsync(Guid jobId, string status, string? errorMessage = null, CancellationToken ct = default);

    /// <summary>通知步驟失敗</summary>
    Task NotifyStepFailedAsync(Guid jobId, Guid stepId, string stepName, string errorMessage, CancellationToken ct = default);
}
