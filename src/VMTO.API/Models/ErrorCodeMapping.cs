namespace VMTO.API.Models;

// ErrorCode 對應 HTTP 狀態碼映射
public static class ErrorCodeMapping
{
    public static int ToHttpStatusCode(string errorCode) => errorCode switch
    {
        "JOB_NOT_FOUND" or "CONN_NOT_FOUND" => 404,
        "JOB_INVALID_TRANSITION" or "JOB_ALREADY_RUNNING" => 409,
        "CONN_VALIDATION_FAILED" => 422,
        "CONN_DUPLICATE" => 409,
        "LIC_INVALID" or "LIC_EXPIRED" or "LIC_LIMIT_EXCEEDED" => 403,
        "UNAUTHORIZED" => 401,
        "FORBIDDEN" => 403,
        "TIMEOUT" => 504,
        _ => 500
    };
}
