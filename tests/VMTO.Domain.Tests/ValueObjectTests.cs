using FluentAssertions;
using VMTO.Domain.Aggregates.Artifact;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.ValueObjects;

namespace VMTO.Domain.Tests;

/// <summary>
/// 值物件的單元測試。
/// 驗證 record 的相等性語義。
/// </summary>
public sealed class ValueObjectTests
{
    #region StorageTarget 相等性測試

    [Fact]
    public void StorageTarget_相同值應相等()
    {
        var a = new StorageTarget(StorageType.S3, "http://minio:9000", "bucket", "us-east-1");
        var b = new StorageTarget(StorageType.S3, "http://minio:9000", "bucket", "us-east-1");

        a.Should().Be(b);
    }

    [Fact]
    public void StorageTarget_不同值應不相等()
    {
        var a = new StorageTarget(StorageType.S3, "http://minio:9000", "bucket-a", "us-east-1");
        var b = new StorageTarget(StorageType.S3, "http://minio:9000", "bucket-b", "us-east-1");

        a.Should().NotBe(b);
    }

    [Fact]
    public void StorageTarget_不同Type應不相等()
    {
        var a = new StorageTarget(StorageType.S3, "http://minio:9000", "bucket", "us-east-1");
        var b = new StorageTarget(StorageType.Local, "http://minio:9000", "bucket", "us-east-1");

        a.Should().NotBe(b);
    }

    #endregion

    #region Checksum 相等性測試

    [Fact]
    public void Checksum_相同值應相等()
    {
        var a = new Checksum("SHA256", "abc123def456");
        var b = new Checksum("SHA256", "abc123def456");

        a.Should().Be(b);
    }

    [Fact]
    public void Checksum_不同值應不相等()
    {
        var a = new Checksum("SHA256", "abc123");
        var b = new Checksum("SHA256", "xyz789");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Checksum_不同演算法應不相等()
    {
        var a = new Checksum("SHA256", "abc123");
        var b = new Checksum("MD5", "abc123");

        a.Should().NotBe(b);
    }

    #endregion

    #region EncryptedSecret 相等性測試

    [Fact]
    public void EncryptedSecret_相同值應相等()
    {
        var a = new EncryptedSecret("cipher-text", "key-id-1");
        var b = new EncryptedSecret("cipher-text", "key-id-1");

        a.Should().Be(b);
    }

    [Fact]
    public void EncryptedSecret_不同CipherText應不相等()
    {
        var a = new EncryptedSecret("cipher-a");
        var b = new EncryptedSecret("cipher-b");

        a.Should().NotBe(b);
    }

    [Fact]
    public void EncryptedSecret_含與不含KeyId應不相等()
    {
        var a = new EncryptedSecret("cipher-text", "key-id");
        var b = new EncryptedSecret("cipher-text");

        a.Should().NotBe(b);
    }

    [Fact]
    public void EncryptedSecret_都無KeyId且CipherText相同應相等()
    {
        var a = new EncryptedSecret("cipher-text");
        var b = new EncryptedSecret("cipher-text");

        a.Should().Be(b);
    }

    #endregion

    #region MigrationOptions 相等性測試

    [Fact]
    public void MigrationOptions_相同值應相等()
    {
        var a = new MigrationOptions(ArtifactFormat.Qcow2, false, true, 3);
        var b = new MigrationOptions(ArtifactFormat.Qcow2, false, true, 3);

        a.Should().Be(b);
    }

    [Fact]
    public void MigrationOptions_不同值應不相等()
    {
        var a = new MigrationOptions(ArtifactFormat.Qcow2, false, true, 3);
        var b = new MigrationOptions(ArtifactFormat.Vmdk, true, false, 5);

        a.Should().NotBe(b);
    }

    [Fact]
    public void MigrationOptions_僅MaxRetries不同應不相等()
    {
        var a = new MigrationOptions(ArtifactFormat.Qcow2, false, true, 3);
        var b = new MigrationOptions(ArtifactFormat.Qcow2, false, true, 5);

        a.Should().NotBe(b);
    }

    #endregion
}
