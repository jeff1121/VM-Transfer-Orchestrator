using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VMTO.Application.Ports.Services;
using VMTO.Infrastructure.Ops;
using VMTO.Infrastructure.Persistence;

namespace VMTO.Infrastructure.Jobs;

public sealed partial class ArtifactCleanupJob(
    AppDbContext db,
    IConfiguration configuration,
    ILogger<ArtifactCleanupJob> logger,
    IOptions<OpsAutomationOptions> options,
    IWebhookService webhookService,
    IAmazonS3? s3Client = null)
{
    private readonly OpsAutomationOptions _options = options.Value;

    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var retentionDays = Math.Max(1, _options.ArtifactRetentionDays);
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var bucket = configuration["Storage:S3:BucketName"] ?? "vmto-artifacts";

        var expired = await db.Artifacts
            .Where(a => a.CreatedAt < cutoff)
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            return 0;
        }

        if (s3Client is not null)
        {
            foreach (var artifact in expired)
            {
                try
                {
                    await s3Client.DeleteObjectAsync(new DeleteObjectRequest
                    {
                        BucketName = bucket,
                        Key = artifact.StorageUri
                    }, ct);
                }
                catch (AmazonS3Exception ex)
                {
                    LogDeleteArtifactObjectFailed(logger, artifact.Id, ex.Message);
                }
            }
        }

        db.Artifacts.RemoveRange(expired);
        await db.SaveChangesAsync(ct);

        await webhookService.NotifySystemAnnouncementAsync("SystemAnnouncement", new
        {
            action = "artifact-cleanup",
            deletedCount = expired.Count,
            retentionDays,
            at = DateTime.UtcNow
        }, ct);

        return expired.Count;
    }

    [LoggerMessage(EventId = 9711, Level = LogLevel.Warning,
        Message = "Delete artifact object failed. ArtifactId={ArtifactId}, Error={Error}")]
    private static partial void LogDeleteArtifactObjectFailed(ILogger logger, Guid artifactId, string error);
}
