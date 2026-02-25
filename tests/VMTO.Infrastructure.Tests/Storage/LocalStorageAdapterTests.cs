using FluentAssertions;
using VMTO.Infrastructure.Storage;
using VMTO.Shared;

namespace VMTO.Infrastructure.Tests.Storage;

public sealed class LocalStorageAdapterTests
{
    [Fact]
    public async Task UploadAsyncShouldFailWhenKeyContainsPathTraversal()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"vmto-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(basePath);

        try
        {
            var sut = new LocalStorageAdapter(basePath);
            await using var stream = new MemoryStream("test"u8.ToArray());

            var result = await sut.UploadAsync("../outside.txt", stream, stream.Length);

            result.IsSuccess.Should().BeFalse();
            result.ErrorCode.Should().Be(ErrorCodes.Storage.UploadFailed);
            File.Exists(Path.Combine(Directory.GetParent(basePath)!.FullName, "outside.txt")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(basePath, true);
        }
    }

    [Fact]
    public async Task UploadAsyncShouldSucceedWhenKeyIsWithinBasePath()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"vmto-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(basePath);

        try
        {
            var sut = new LocalStorageAdapter(basePath);
            await using var stream = new MemoryStream("ok"u8.ToArray());

            var result = await sut.UploadAsync("folder/file.txt", stream, stream.Length);

            result.IsSuccess.Should().BeTrue();
            File.Exists(Path.Combine(basePath, "folder", "file.txt")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(basePath, true);
        }
    }
}
