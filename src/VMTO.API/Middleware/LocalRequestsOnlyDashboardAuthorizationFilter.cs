using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace VMTO.API.Middleware;

/// <summary>
/// Hangfire Dashboard 授權過濾器：僅允許來自本地端的請求存取儀表板。
/// 生產環境應改用 JWT-based 過濾器。
/// </summary>
public sealed class LocalRequestsOnlyDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize([NotNull] DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.Connection.RemoteIpAddress is { } ip
            && (System.Net.IPAddress.IsLoopback(ip) ||
                ip.Equals(httpContext.Connection.LocalIpAddress));
    }
}
