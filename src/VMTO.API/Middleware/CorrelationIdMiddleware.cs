using Serilog.Context;

namespace VMTO.API.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var headerValue = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = (headerValue is not null && IsValidCorrelationId(headerValue))
            ? headerValue
            : Guid.NewGuid().ToString("D");

        context.Items["CorrelationId"] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    private static bool IsValidCorrelationId(string value) =>
        !string.IsNullOrEmpty(value) &&
        value.Length <= 64 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');
}
