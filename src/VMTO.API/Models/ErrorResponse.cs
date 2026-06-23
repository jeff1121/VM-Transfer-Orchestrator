namespace VMTO.API.Models;

// 統一錯誤回應格式
public sealed record ErrorResponse(
    string Code,
    string Message,
    string? CorrelationId = null,
    IDictionary<string, string[]>? Details = null);
