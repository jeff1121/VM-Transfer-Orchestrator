using Microsoft.AspNetCore.Diagnostics;
using VMTO.API.Models;
using VMTO.Shared;

namespace VMTO.API.Middleware;

public sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items["CorrelationId"] as string;

        LogUnhandledException(logger, correlationId, exception);

        // 改用 ErrorResponse 格式
        var error = new ErrorResponse(
            ErrorCodes.General.InternalError,
            "An unexpected error occurred",
            correlationId);

        httpContext.Response.StatusCode = 500;
        await httpContext.Response.WriteAsJsonAsync(error, cancellationToken);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception. CorrelationId={CorrelationId}")]
    private static partial void LogUnhandledException(ILogger logger, string? correlationId, Exception exception);
}
