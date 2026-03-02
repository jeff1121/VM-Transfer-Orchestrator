using VMTO.API.Auth;
using VMTO.API.Models;

namespace VMTO.API.Endpoints;

// 認證端點
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth").AllowAnonymous().RequireRateLimiting("auth");
        group.MapPost("/login", Login);
    }

    private static IResult Login(LoginRequest request, IJwtTokenService tokenService, HttpContext context)
    {
        // 簡易驗證 — 生產環境應替換為真正的使用者驗證邏輯
        // 目前接受任何非空的帳號密碼，預設角色為 Operator
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            var correlationId = context.Items["CorrelationId"] as string;
            return Results.Json(
                new ErrorResponse("AUTH_INVALID_CREDENTIALS", "使用者名稱或密碼不得為空", correlationId),
                statusCode: 401);
        }

        // 預設角色映射（placeholder）
        var role = request.UserName.Equals("admin", StringComparison.OrdinalIgnoreCase)
            ? Roles.Admin
            : Roles.Operator;

        var token = tokenService.GenerateToken(
            Guid.NewGuid().ToString(),
            request.UserName,
            role);

        return Results.Ok(new LoginResponse(token, role, DateTime.UtcNow.AddMinutes(60)));
    }
}

public sealed record LoginRequest(string UserName, string Password);
public sealed record LoginResponse(string Token, string Role, DateTime ExpiresAt);
