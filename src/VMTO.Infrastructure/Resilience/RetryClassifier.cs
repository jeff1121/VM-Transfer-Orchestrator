using Amazon.Runtime;
using Amazon.S3;
using Polly.Timeout;

namespace VMTO.Infrastructure.Resilience;

public static class RetryClassifier
{
    public static bool IsVsphereRetryable(Exception ex) => IsCommonRetryable(ex);

    public static bool IsPveRetryable(Exception ex) => IsCommonRetryable(ex);

    public static bool IsS3Retryable(Exception ex)
    {
        if (IsCommonRetryable(ex))
        {
            return true;
        }

        if (ex is AmazonS3Exception s3Ex)
        {
            var statusCode = (int)s3Ex.StatusCode;
            return statusCode == 0 || statusCode == 429 || statusCode >= 500;
        }

        if (ex is AmazonServiceException awsEx)
        {
            var statusCode = (int)awsEx.StatusCode;
            return statusCode == 0 || statusCode == 429 || statusCode >= 500;
        }

        return false;
    }

    private static bool IsCommonRetryable(Exception ex)
    {
        if (ex is TimeoutException or TaskCanceledException or IOException)
        {
            return true;
        }

        if (ex is TimeoutRejectedException)
        {
            return true;
        }

        if (ex is HttpRequestException httpEx)
        {
            if (httpEx.StatusCode is null)
            {
                return true;
            }

            var statusCode = (int)httpEx.StatusCode;
            return statusCode == 429 || statusCode >= 500;
        }

        return false;
    }
}
