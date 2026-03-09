using System.Diagnostics;
using System.Globalization;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using VMTO.Application.Ports.Services;
using VMTO.Infrastructure.Ops;

namespace VMTO.Infrastructure.Jobs;

public sealed partial class DatabaseBackupJob(
    IConfiguration configuration,
    IOptions<OpsAutomationOptions> options,
    ILogger<DatabaseBackupJob> logger,
    IWebhookService webhookService,
    IAmazonS3? s3Client = null)
{
    private readonly OpsAutomationOptions _options = options.Value;

    public async Task<string?> RunAsync(CancellationToken ct = default)
    {
        if (s3Client is null)
        {
            LogSkipped(logger, "S3 client is not configured.");
            return null;
        }

        var connectionString = configuration.GetConnectionString("PostgreSQL");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            LogSkipped(logger, "ConnectionStrings:PostgreSQL is missing.");
            return null;
        }

        var csb = new NpgsqlConnectionStringBuilder(connectionString);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var tempFile = Path.Combine(Path.GetTempPath(), $"vmto-db-backup-{timestamp}.sql");
        var bucket = _options.BackupBucketName;
        var key = $"db/{DateTime.UtcNow:yyyy/MM/dd}/vmto-{timestamp}.sql";

        try
        {
            await RunPgDumpAsync(csb, tempFile, ct);
            await EnsureBucketAsync(bucket, ct);

            await using var stream = File.OpenRead(tempFile);
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = stream,
                ContentType = "application/sql"
            }, ct);

            await webhookService.NotifySystemAnnouncementAsync("SystemAnnouncement", new
            {
                action = "database-backup",
                bucket,
                key,
                generatedAt = DateTime.UtcNow
            }, ct);

            return key;
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private async Task RunPgDumpAsync(NpgsqlConnectionStringBuilder csb, string outputFile, CancellationToken ct)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "pg_dump",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.StartInfo.ArgumentList.Add("--host");
        process.StartInfo.ArgumentList.Add(csb.Host ?? "localhost");
        process.StartInfo.ArgumentList.Add("--port");
        process.StartInfo.ArgumentList.Add(csb.Port.ToString(CultureInfo.InvariantCulture));
        process.StartInfo.ArgumentList.Add("--username");
        process.StartInfo.ArgumentList.Add(csb.Username ?? "postgres");
        process.StartInfo.ArgumentList.Add("--dbname");
        process.StartInfo.ArgumentList.Add(csb.Database ?? "postgres");
        process.StartInfo.ArgumentList.Add("--file");
        process.StartInfo.ArgumentList.Add(outputFile);
        process.StartInfo.Environment["PGPASSWORD"] = csb.Password ?? string.Empty;

        process.Start();
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(30, _options.DatabaseBackupTimeoutSeconds)));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw new TimeoutException("pg_dump timed out.");
        }

        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"pg_dump failed with code {process.ExitCode}: {stderr}");
        }
    }

    private async Task EnsureBucketAsync(string bucketName, CancellationToken ct)
    {
        var client = s3Client ?? throw new InvalidOperationException("S3 client is not configured.");
        if (await AmazonS3Util.DoesS3BucketExistV2Async(client, bucketName))
        {
            return;
        }

        await client.PutBucketAsync(new PutBucketRequest
        {
            BucketName = bucketName
        }, ct);
    }

    [LoggerMessage(EventId = 9721, Level = LogLevel.Warning, Message = "Database backup skipped: {Reason}")]
    private static partial void LogSkipped(ILogger logger, string reason);
}
