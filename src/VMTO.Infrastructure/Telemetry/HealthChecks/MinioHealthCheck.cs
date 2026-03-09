using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace VMTO.Infrastructure.Telemetry.HealthChecks;

public sealed class MinioHealthCheck(
    IConfiguration configuration,
    IAmazonS3? s3Client = null) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (s3Client is null)
        {
            return HealthCheckResult.Healthy("S3/MinIO client is not configured.");
        }

        var bucketName = configuration["Storage:S3:BucketName"];
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            return HealthCheckResult.Unhealthy("Storage:S3:BucketName is missing.");
        }

        var tempKey = $"healthchecks/{Guid.NewGuid():N}.txt";
        try
        {
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = tempKey,
                ContentBody = "healthcheck"
            }, cancellationToken);

            await s3Client.DeleteObjectAsync(bucketName, tempKey, cancellationToken);
            return HealthCheckResult.Healthy("MinIO bucket is writable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Unable to verify MinIO bucket access.", ex);
        }
    }
}
