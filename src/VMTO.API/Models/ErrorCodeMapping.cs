namespace VMTO.API.Models;

// ErrorCode 對應 HTTP 狀態碼映射
// TODO: 未來可透過前端 i18n 機制將錯誤碼對應至各語系的使用者友善訊息，
//       後端僅回傳 errorCode，由前端根據 locale 顯示對應文字。
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
