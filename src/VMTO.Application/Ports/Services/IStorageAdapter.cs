using VMTO.Shared;

namespace VMTO.Application.Ports.Services;

public interface IStorageAdapter
{
    Task<Result> UploadAsync(string key, Stream content, long contentLength, string? contentType = null, CancellationToken ct = default);
    Task<Result<Stream>> DownloadAsync(string key, CancellationToken ct = default);
    Task<Result> DeleteAsync(string key, CancellationToken ct = default);
    Task<Result<bool>> ExistsAsync(string key, CancellationToken ct = default);
    Task<Result<string>> GetChecksumAsync(string key, CancellationToken ct = default);
}
