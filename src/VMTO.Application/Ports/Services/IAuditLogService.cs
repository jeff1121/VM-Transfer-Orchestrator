namespace VMTO.Application.Ports.Services;

public interface IAuditLogService
{
    Task LogAsync(string action, string entityType, Guid entityId, string? userId = null, string? details = null, CancellationToken ct = default);
}
