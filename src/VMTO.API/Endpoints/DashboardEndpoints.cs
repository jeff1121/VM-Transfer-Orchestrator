using Microsoft.EntityFrameworkCore;
using VMTO.Domain.Enums;
using VMTO.Infrastructure.Persistence;

namespace VMTO.API.Endpoints;

/// <summary>儀表板統計端點</summary>
public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard").WithTags("Dashboard").RequireAuthorization();

        group.MapGet("/stats", GetStats);
    }

    /// <summary>取得遷移統計資料</summary>
    private static async Task<IResult> GetStats(AppDbContext db, CancellationToken ct)
    {
        var jobs = db.Jobs.AsNoTracking();

        // 各狀態任務數量
        var statusCounts = await jobs
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status.ToString(), x => x.Count, ct);

        // 確保所有狀態皆有值
        foreach (var s in Enum.GetValues<JobStatus>())
        {
            statusCounts.TryAdd(s.ToString(), 0);
        }

        // 最近 30 天每日趨勢
        var since = DateTime.UtcNow.Date.AddDays(-29);
        var dailyRaw = await jobs
            .Where(j => j.CreatedAt >= since)
            .GroupBy(j => j.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Succeeded = g.Count(j => j.Status == JobStatus.Succeeded),
                Failed = g.Count(j => j.Status == JobStatus.Failed),
                Total = g.Count()
            })
            .ToListAsync(ct);

        var dailyLookup = dailyRaw.ToDictionary(d => d.Date);
        var dailyTrend = Enumerable.Range(0, 30)
            .Select(i => since.AddDays(i))
            .Select(date => dailyLookup.TryGetValue(date, out var d)
                ? new DailyTrendDto(date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), d.Succeeded, d.Failed, d.Total)
                : new DailyTrendDto(date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), 0, 0, 0))
            .ToList();

        // 總任務數
        var totalJobs = await jobs.CountAsync(ct);

        // 已完成任務的平均耗時（分鐘）
        var succeededJobs = await jobs
            .Where(j => j.Status == JobStatus.Succeeded)
            .Select(j => new { j.CreatedAt, j.UpdatedAt })
            .ToListAsync(ct);

        var avgDuration = succeededJobs.Count > 0
            ? succeededJobs.Average(j => (j.UpdatedAt - j.CreatedAt).TotalSeconds)
            : 0;

        var stats = new DashboardStatsDto(
            statusCounts,
            dailyTrend,
            totalJobs,
            TotalTransferredBytes: 0,
            AverageDurationMinutes: Math.Round(avgDuration / 60.0, 1));

        return Results.Ok(stats);
    }
}

/// <summary>儀表板統計 DTO</summary>
public sealed record DashboardStatsDto(
    Dictionary<string, int> StatusCounts,
    List<DailyTrendDto> DailyTrend,
    int TotalJobs,
    long TotalTransferredBytes,
    double AverageDurationMinutes);

/// <summary>每日趨勢 DTO</summary>
public sealed record DailyTrendDto(string Date, int Succeeded, int Failed, int Total);
