using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VMTO.API.Auth;
using VMTO.Infrastructure.Persistence;

namespace VMTO.API.Endpoints;

// 稽核日誌 DTO
public sealed record AuditLogDto(
    Guid Id,
    string Action,
    string EntityType,
    Guid EntityId,
    string? UserId,
    string? Details,
    DateTime CreatedAt);

// 分頁回應
public sealed record AuditListResponse(
    AuditLogDto[] Items,
    int Total,
    int Page,
    int PageSize);

// 統計摘要
public sealed record AuditSummaryResponse(
    int TotalCount,
    Dictionary<string, int> ActionCounts,
    int RecentCount);

public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit")
            .WithTags("Audit")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        // 分頁查詢稽核日誌
        group.MapGet("/", ListAuditLogs);

        // 匯出稽核日誌為 CSV
        group.MapGet("/export", ExportCsv);

        // 稽核統計摘要
        group.MapGet("/summary", GetSummary);
    }

    private static async Task<IResult> ListAuditLogs(
        AppDbContext db,
        int page = 1,
        int pageSize = 20,
        string? action = null,
        string? entityType = null,
        string? userId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = ApplyFilters(db, action, entityType, userId, from, to);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AuditLogDto(e.Id, e.Action, e.EntityType, e.EntityId, e.UserId, e.Details, e.CreatedAt))
            .ToArrayAsync(ct);

        return Results.Ok(new AuditListResponse(items, total, page, pageSize));
    }

    private static async Task<IResult> ExportCsv(
        AppDbContext db,
        string? action = null,
        string? entityType = null,
        string? userId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        var query = ApplyFilters(db, action, entityType, userId, from, to);

        var entries = await query
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new AuditLogDto(e.Id, e.Action, e.EntityType, e.EntityId, e.UserId, e.Details, e.CreatedAt))
            .ToListAsync(ct);

        var sb = new StringBuilder();
        // UTF-8 BOM 讓 Excel 正確識別編碼
        sb.Append("時間,動作,實體類型,實體ID,使用者,詳細資訊\r\n");
        foreach (var e in entries)
        {
            sb.Append(CsvEscape(e.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
            sb.Append(',');
            sb.Append(CsvEscape(e.Action));
            sb.Append(',');
            sb.Append(CsvEscape(e.EntityType));
            sb.Append(',');
            sb.Append(CsvEscape(e.EntityId.ToString()));
            sb.Append(',');
            sb.Append(CsvEscape(e.UserId ?? ""));
            sb.Append(',');
            sb.Append(CsvEscape(e.Details ?? ""));
            sb.Append("\r\n");
        }

        // 加入 UTF-8 BOM
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[bom.Length + csvBytes.Length];
        bom.CopyTo(result, 0);
        csvBytes.CopyTo(result, bom.Length);

        return Results.File(result, "text/csv", "audit-logs.csv");
    }

    private static async Task<IResult> GetSummary(
        AppDbContext db,
        CancellationToken ct = default)
    {
        var totalCount = await db.AuditLogs.CountAsync(ct);

        var since = DateTime.UtcNow.AddDays(-30);
        var recentQuery = db.AuditLogs.Where(e => e.CreatedAt >= since);

        var recentCount = await recentQuery.CountAsync(ct);

        var actionCounts = await recentQuery
            .GroupBy(e => e.Action)
            .Select(g => new { Action = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Action, x => x.Count, ct);

        return Results.Ok(new AuditSummaryResponse(totalCount, actionCounts, recentCount));
    }

    private static IQueryable<Infrastructure.Persistence.Entities.AuditLogEntry> ApplyFilters(
        AppDbContext db,
        string? action,
        string? entityType,
        string? userId,
        DateTime? from,
        DateTime? to)
    {
        var query = db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(e => e.Action == action);

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(e => e.EntityType == entityType);

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(e => e.UserId == userId);

        if (from.HasValue)
            query = query.Where(e => e.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.CreatedAt <= to.Value);

        return query;
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
