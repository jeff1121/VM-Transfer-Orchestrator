using System.Security.Cryptography;
using VMTO.Application.Ports.Services;
using VMTO.Shared;

namespace VMTO.Infrastructure.Storage;

public sealed class LocalStorageAdapter : IStorageAdapter
{
    private readonly string _basePath;

    public LocalStorageAdapter(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(_basePath);
    }

    public async Task<Result> UploadAsync(string key, Stream content, long contentLength, string? contentType = null, CancellationToken ct = default)
    {
        try
        {
            var filePath = GetFullPath(key);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            await using var fileStream = File.Create(filePath);
            await content.CopyToAsync(fileStream, ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ErrorCodes.Storage.UploadFailed, $"Local upload failed: {ex.Message}");
        }
    }

    public Task<Result<Stream>> DownloadAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var filePath = GetFullPath(key);
            if (!File.Exists(filePath))
                return Task.FromResult(Result<Stream>.Failure(ErrorCodes.Storage.DownloadFailed, $"File not found: {key}"));

            Stream stream = File.OpenRead(filePath);
            return Task.FromResult(Result<Stream>.Success(stream));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<Stream>.Failure(ErrorCodes.Storage.DownloadFailed, $"Local download failed: {ex.Message}"));
        }
    }

    public Task<Result> DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var filePath = GetFullPath(key);
            if (File.Exists(filePath))
                File.Delete(filePath);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure(ErrorCodes.General.InternalError, $"Local delete failed: {ex.Message}"));
        }
    }

    public Task<Result<bool>> ExistsAsync(string key, CancellationToken ct = default)
    {
        var exists = File.Exists(GetFullPath(key));
        return Task.FromResult(Result<bool>.Success(exists));
    }

    public async Task<Result<string>> GetChecksumAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var filePath = GetFullPath(key);
            if (!File.Exists(filePath))
                return Result<string>.Failure(ErrorCodes.Storage.DownloadFailed, $"File not found: {key}");

            await using var stream = File.OpenRead(filePath);
            var hash = await SHA256.HashDataAsync(stream, ct);
            return Result<string>.Success(Convert.ToHexStringLower(hash));
        }
        catch (Exception ex)
        {
            return Result<string>.Failure(ErrorCodes.General.InternalError, $"Checksum failed: {ex.Message}");
        }
    }

    private string GetFullPath(string key) => Path.Combine(_basePath, key);
}
