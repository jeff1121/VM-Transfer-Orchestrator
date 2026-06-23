using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using VMTO.Application.Ports.Services;
using VMTO.Infrastructure.Ops;

namespace VMTO.Infrastructure.Jobs;

public sealed class StorageUsageJob(
    IConfiguration configuration,
    IOpsSnapshotStore snapshotStore,
    IWebhookService webhookService,
    IOptions<OpsAutomationOptions> options,
    IAmazonS3? s3Client = null)
{
    private readonly OpsAutomationOptions _options = options.Value;

    public async Task<StorageUsageSnapshot> RunAsync(CancellationToken ct = default)
    {
        var bucket = configuration["Storage:S3:BucketName"] ?? "vmto-artifacts";
        if (s3Client is null)
        {
            var emptySnapshot = new StorageUsageSnapshot(DateTime.UtcNow, bucket, 0, 0, false);
            snapshotStore.AddStorageSnapshot(emptySnapshot);
            return emptySnapshot;
        }

        long totalBytes = 0;
        var totalObjects = 0;
        string? token = null;

        do
        {
            var response = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucket,
                ContinuationToken = token
            }, ct);

            totalObjects += response.S3Objects.Count;
            totalBytes += response.S3Objects.Sum(x => x.Size ?? 0L);
            token = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (!string.IsNullOrEmpty(token));

        var snapshot = new StorageUsageSnapshot(
            DateTime.UtcNow,
            bucket,
            totalBytes,
            totalObjects,
            totalBytes >= _options.StorageUsageThresholdBytes);

        snapshotStore.AddStorageSnapshot(snapshot);
        if (snapshot.ThresholdExceeded)
        {
            await webhookService.NotifySystemAnnouncementAsync("SystemAnnouncement", new
            {
                action = "storage-usage-alert",
                bucket,
                usedBytes = totalBytes,
                thresholdBytes = _options.StorageUsageThresholdBytes,
                objectCount = totalObjects,
                at = DateTime.UtcNow
            }, ct);
        }

        return snapshot;
    }
}
