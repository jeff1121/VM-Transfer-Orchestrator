using Amazon.S3;
using Amazon.S3.Model;
using VMTO.Application.Ports.Services;
using VMTO.Shared;

namespace VMTO.Infrastructure.Storage;

public sealed class S3StorageAdapter : IStorageAdapter
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;
    private const long MultipartThreshold = 100 * 1024 * 1024; // 100 MB

    public S3StorageAdapter(IAmazonS3 s3, string bucketName)
    {
        _s3 = s3;
        _bucketName = bucketName;
    }

    public async Task<Result> UploadAsync(string key, Stream content, long contentLength, string? contentType = null, CancellationToken ct = default)
    {
        try
        {
            if (contentLength > MultipartThreshold)
                return await MultipartUploadAsync(key, content, contentLength, contentType, ct);

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = content,
                ContentType = contentType ?? "application/octet-stream",
            };

            await _s3.PutObjectAsync(request, ct);
            return Result.Success();
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
            var response = await _s3.GetObjectAsync(_bucketName, key, ct);
            return Result<Stream>.Success(response.ResponseStream);
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
            await _s3.DeleteObjectAsync(_bucketName, key, ct);
            return Result.Success();
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
            await _s3.GetObjectMetadataAsync(_bucketName, key, ct);
            return Result<bool>.Success(true);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Result<bool>.Success(false);
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
            var metadata = await _s3.GetObjectMetadataAsync(_bucketName, key, ct);
            var etag = metadata.ETag?.Trim('"') ?? string.Empty;
            return Result<string>.Success(etag);
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
