using Amazon.S3;
using Amazon.S3.Model;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using VMTO.Application.Ports.Services;
using VMTO.Infrastructure.Resilience;
using VMTO.Shared;
using VMTO.Shared.Telemetry;

namespace VMTO.Infrastructure.Storage;

public sealed class S3StorageAdapter : IStorageAdapter
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;
    private readonly ResiliencePipeline _pipeline;
    private readonly IChaosPolicy _chaosPolicy;
    private const long MultipartThreshold = 100 * 1024 * 1024; // 100 MB

    public S3StorageAdapter(
        IAmazonS3 s3,
        string bucketName,
        CircuitBreakerNotifier notifier,
        RetryPolicyOptions retryPolicyOptions,
        IChaosPolicy chaosPolicy)
    {
        _s3 = s3;
        _bucketName = bucketName;
        _chaosPolicy = chaosPolicy;
        _pipeline = CircuitBreakerPipelineFactory.Create(
            serviceName: "s3",
            minimumThroughput: 3,
            breakDuration: TimeSpan.FromSeconds(60),
            retryOptions: retryPolicyOptions,
            retryClassifier: RetryClassifier.IsS3Retryable,
            notifier);
    }

    public async Task<Result> UploadAsync(string key, Stream content, long contentLength, string? contentType = null, CancellationToken ct = default)
    {
        using var activity = ActivitySources.Default.StartActivity("s3.upload", System.Diagnostics.ActivityKind.Client);
        activity?.SetTag("vmto.s3.bucket", _bucketName);
        activity?.SetTag("vmto.s3.key", key);
        activity?.SetTag("vmto.s3.content_length", contentLength);

        try
        {
            await _chaosPolicy.ApplyAsync("s3.upload", ct);
            return await _pipeline.ExecuteAsync(async token =>
            {
                if (contentLength > MultipartThreshold)
                {
                    activity?.SetTag("vmto.s3.multipart", true);
                    return await MultipartUploadAsync(key, content, contentLength, contentType, token);
                }

                activity?.SetTag("vmto.s3.multipart", false);
                activity?.SetTag("vmto.s3.chunk.number", 1);
                activity?.SetTag("vmto.s3.chunk.size_bytes", contentLength);

                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key,
                    InputStream = content,
                    ContentType = contentType ?? "application/octet-stream",
                };

                await _s3.PutObjectAsync(request, token);
                VmtoMetrics.AddTransferBytes(contentLength);
                return Result.Success();
            }, ct);
        }
        catch (TimeoutRejectedException ex)
        {
            return Result.Failure(ErrorCodes.General.ExternalCommandFailed, $"S3 request timed out: {ex.Message}");
        }
        catch (BrokenCircuitException ex)
        {
            return Result.Failure(ErrorCodes.General.ExternalCommandFailed, $"S3 circuit breaker is open: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result.Failure(ErrorCodes.Storage.UploadFailed, $"S3 upload failed: {ex.Message}");
        }
    }

    public async Task<Result<Stream>> DownloadAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _chaosPolicy.ApplyAsync("s3.download", ct);
            return await _pipeline.ExecuteAsync(async token =>
            {
                var response = await _s3.GetObjectAsync(_bucketName, key, token);
                return Result<Stream>.Success(response.ResponseStream);
            }, ct);
        }
        catch (TimeoutRejectedException ex)
        {
            return Result<Stream>.Failure(ErrorCodes.General.ExternalCommandFailed, $"S3 request timed out: {ex.Message}");
        }
        catch (BrokenCircuitException ex)
        {
            return Result<Stream>.Failure(ErrorCodes.General.ExternalCommandFailed, $"S3 circuit breaker is open: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<Stream>.Failure(ErrorCodes.Storage.DownloadFailed, $"S3 download failed: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _chaosPolicy.ApplyAsync("s3.delete", ct);
            return await _pipeline.ExecuteAsync(async token =>
            {
                await _s3.DeleteObjectAsync(_bucketName, key, token);
                return Result.Success();
            }, ct);
        }
        catch (TimeoutRejectedException ex)
        {
            return Result.Failure(ErrorCodes.General.ExternalCommandFailed, $"S3 request timed out: {ex.Message}");
        }
        catch (BrokenCircuitException ex)
        {
            return Result.Failure(ErrorCodes.General.ExternalCommandFailed, $"S3 circuit breaker is open: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result.Failure(ErrorCodes.General.InternalError, $"S3 delete failed: {ex.Message}");
        }
    }

    public async Task<Result<bool>> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _chaosPolicy.ApplyAsync("s3.exists", ct);
            return await _pipeline.ExecuteAsync(async token =>
            {
                await _s3.GetObjectMetadataAsync(_bucketName, key, token);
                return Result<bool>.Success(true);
            }, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Result<bool>.Success(false);
        }
        catch (TimeoutRejectedException ex)
        {
            return Result<bool>.Failure(ErrorCodes.General.ExternalCommandFailed, $"S3 request timed out: {ex.Message}");
        }
        catch (BrokenCircuitException ex)
        {
            return Result<bool>.Failure(ErrorCodes.General.ExternalCommandFailed, $"S3 circuit breaker is open: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(ErrorCodes.General.InternalError, $"S3 exists check failed: {ex.Message}");
        }
    }

    public async Task<Result<string>> GetChecksumAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _chaosPolicy.ApplyAsync("s3.checksum", ct);
            return await _pipeline.ExecuteAsync(async token =>
            {
                var metadata = await _s3.GetObjectMetadataAsync(_bucketName, key, token);
                var etag = metadata.ETag?.Trim('"') ?? string.Empty;
                return Result<string>.Success(etag);
            }, ct);
        }
        catch (TimeoutRejectedException ex)
        {
            return Result<string>.Failure(ErrorCodes.General.ExternalCommandFailed, $"S3 request timed out: {ex.Message}");
        }
        catch (BrokenCircuitException ex)
        {
            return Result<string>.Failure(ErrorCodes.General.ExternalCommandFailed, $"S3 circuit breaker is open: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure(ErrorCodes.General.InternalError, $"S3 checksum failed: {ex.Message}");
        }
    }

    private async Task<Result> MultipartUploadAsync(string key, Stream content, long contentLength, string? contentType, CancellationToken ct)
    {
        const long partSize = 50 * 1024 * 1024; // 50 MB parts
        var initRequest = new InitiateMultipartUploadRequest
        {
            BucketName = _bucketName,
            Key = key,
            ContentType = contentType ?? "application/octet-stream",
        };

        var initResponse = await _s3.InitiateMultipartUploadAsync(initRequest, ct);
        var uploadId = initResponse.UploadId;

        try
        {
            var partETags = new List<PartETag>();
            var partNumber = 1;
            var buffer = new byte[partSize];

            while (true)
            {
                var bytesRead = await ReadFullBufferAsync(content, buffer, ct);
                if (bytesRead == 0)
                    break;

                using var partActivity = ActivitySources.Default.StartActivity("s3.upload.part", System.Diagnostics.ActivityKind.Client);
                partActivity?.SetTag("vmto.s3.bucket", _bucketName);
                partActivity?.SetTag("vmto.s3.key", key);
                partActivity?.SetTag("vmto.s3.chunk.number", partNumber);
                partActivity?.SetTag("vmto.s3.chunk.size_bytes", bytesRead);

                using var partStream = new MemoryStream(buffer, 0, bytesRead);
                var uploadPartRequest = new UploadPartRequest
                {
                    BucketName = _bucketName,
                    Key = key,
                    UploadId = uploadId,
                    PartNumber = partNumber,
                    InputStream = partStream,
                };

                var partResponse = await _s3.UploadPartAsync(uploadPartRequest, ct);
                partETags.Add(new PartETag(partNumber, partResponse.ETag));
                VmtoMetrics.AddTransferBytes(bytesRead);
                partNumber++;
            }

            var completeRequest = new CompleteMultipartUploadRequest
            {
                BucketName = _bucketName,
                Key = key,
                UploadId = uploadId,
                PartETags = partETags,
            };

            await _s3.CompleteMultipartUploadAsync(completeRequest, ct);
            return Result.Success();
        }
        catch
        {
            await _s3.AbortMultipartUploadAsync(_bucketName, key, uploadId, ct);
            throw;
        }
    }

    private static async Task<int> ReadFullBufferAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead), ct);
            if (read == 0)
                break;
            totalRead += read;
        }
        return totalRead;
    }
}
