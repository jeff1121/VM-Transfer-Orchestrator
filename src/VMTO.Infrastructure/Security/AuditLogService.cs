using VMTO.Infrastructure.Persistence;
using VMTO.Infrastructure.Persistence.Entities;

namespace VMTO.Infrastructure.Security;

public interface IAuditLogService
{
    Task LogAsync(string action, string entityType, Guid entityId, string? userId = null, string? details = null, CancellationToken ct = default);
}

public sealed class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _db;

    public AuditLogService(AppDbContext db) => _db = db;

    public async Task LogAsync(string action, string entityType, Guid entityId, string? userId = null, string? details = null, CancellationToken ct = default)
    {
        var entry = new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            UserId = userId,
            Details = details,
            CreatedAt = DateTime.UtcNow,
        };

        await _db.AuditLogs.AddAsync(entry, ct);
        await _db.SaveChangesAsync(ct);
    }
}
