using VMTO.API.Models;
using VMTO.Shared;

namespace VMTO.API.Extensions;

// Result / Result<T> 轉換為 HTTP 回應的擴充方法
public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result, HttpContext context)
    {
        if (result.IsSuccess)
            return Results.Ok();

        var correlationId = context.Items["CorrelationId"] as string;
        var statusCode = ErrorCodeMapping.ToHttpStatusCode(result.ErrorCode!);
        return Results.Json(
            new ErrorResponse(result.ErrorCode!, result.ErrorMessage!, correlationId),
            statusCode: statusCode);
    }

    public static IResult ToHttpResult<T>(this Result<T> result, HttpContext context, Func<T, object>? transform = null)
    {
        if (result.IsSuccess)
            return Results.Ok(transform != null ? transform(result.Value!) : result.Value);

        var correlationId = context.Items["CorrelationId"] as string;
        var statusCode = ErrorCodeMapping.ToHttpStatusCode(result.ErrorCode!);
        return Results.Json(
            new ErrorResponse(result.ErrorCode!, result.ErrorMessage!, correlationId),
            statusCode: statusCode);
    }

    public static IResult ToCreatedResult<T>(this Result<T> result, HttpContext context, string locationPath)
    {
        if (result.IsSuccess)
            return Results.Created(locationPath, result.Value);

        var correlationId = context.Items["CorrelationId"] as string;
        var statusCode = ErrorCodeMapping.ToHttpStatusCode(result.ErrorCode!);
        return Results.Json(
            new ErrorResponse(result.ErrorCode!, result.ErrorMessage!, correlationId),
            statusCode: statusCode);
    }
}
