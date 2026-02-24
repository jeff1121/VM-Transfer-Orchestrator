using VMTO.Domain.Aggregates.Artifact;
using VMTO.Shared;

namespace VMTO.Application.Ports.Services;

public interface IQemuImgService
{
    Task<Result> ConvertAsync(string inputPath, string outputPath, ArtifactFormat targetFormat, IProgress<int>? progress = null, CancellationToken ct = default);
    Task<Result<string>> GetInfoAsync(string imagePath, CancellationToken ct = default);
}
