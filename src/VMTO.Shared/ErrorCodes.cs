namespace VMTO.Shared;

public static class ErrorCodes
{
    public static class Job
    {
        public const string NotFound = "JOB_NOT_FOUND";
        public const string InvalidTransition = "JOB_INVALID_TRANSITION";
        public const string AlreadyRunning = "JOB_ALREADY_RUNNING";
        public const string CancellationFailed = "JOB_CANCELLATION_FAILED";
    }

    public static class Connection
    {
        public const string NotFound = "CONN_NOT_FOUND";
        public const string ValidationFailed = "CONN_VALIDATION_FAILED";
        public const string Duplicate = "CONN_DUPLICATE";
    }

    public static class Storage
    {
        public const string UploadFailed = "STOR_UPLOAD_FAILED";
        public const string DownloadFailed = "STOR_DOWNLOAD_FAILED";
        public const string ChecksumMismatch = "STOR_CHECKSUM_MISMATCH";
    }

    public static class License
    {
        public const string Invalid = "LIC_INVALID";
        public const string Expired = "LIC_EXPIRED";
        public const string LimitExceeded = "LIC_LIMIT_EXCEEDED";
    }

    public static class General
    {
        public const string Unauthorized = "UNAUTHORIZED";
        public const string Forbidden = "FORBIDDEN";
        public const string InternalError = "INTERNAL_ERROR";
        public const string ExternalCommandFailed = "EXTERNAL_CMD_FAILED";
        public const string Timeout = "TIMEOUT";
    }
}
