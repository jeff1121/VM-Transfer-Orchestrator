namespace VMTO.Infrastructure.Resilience;

public sealed class ErrorClassifier : IErrorClassifier
{
    private static readonly string[] TransientKeywords =
    [
        "timeout",
        "timed out",
        "connection reset",
        "temporarily unavailable",
        "503",
        "502",
        "429",
        "network",
        "econnreset",
        "broken pipe"
    ];

    private static readonly string[] PermanentKeywords =
    [
        "disk full",
        "permission denied",
        "unauthorized",
        "forbidden",
        "invalid",
        "not found",
        "400",
        "404"
    ];

    public ErrorCategory Classify(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return ErrorCategory.Unknown;
        }

        var normalized = errorMessage.ToLowerInvariant();
        if (TransientKeywords.Any(normalized.Contains))
        {
            return ErrorCategory.Transient;
        }

        if (PermanentKeywords.Any(normalized.Contains))
        {
            return ErrorCategory.Permanent;
        }

        return ErrorCategory.Unknown;
    }
}
