using FluentAssertions;
using VMTO.Domain.Aggregates.Connection;
using VMTO.Domain.ValueObjects;

namespace VMTO.Domain.Tests;

/// <summary>
/// Connection 聚合根的單元測試。
/// 驗證建構、驗證標記及密鑰更新行為。
/// </summary>
public sealed class ConnectionTests
{
    private static Connection CreateConnection()
        => new("vcenter-prod", ConnectionType.VSphere, "https://vcenter.local", new EncryptedSecret("cipher-text", "key-1"));

    #region 建構子測試

    [Fact]
    public void Constructor_應正確設定所有屬性且ValidatedAt為null()
    {
        var secret = new EncryptedSecret("cipher-text", "key-1");

        var conn = new Connection("vcenter-prod", ConnectionType.VSphere, "https://vcenter.local", secret);

        conn.Id.Should().NotBeEmpty();
        conn.Name.Should().Be("vcenter-prod");
        conn.Type.Should().Be(ConnectionType.VSphere);
        conn.Endpoint.Should().Be("https://vcenter.local");
        conn.EncryptedSecret.Should().Be(secret);
        conn.ValidatedAt.Should().BeNull();
        conn.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    #endregion

    #region MarkValidated 測試

    [Fact]
    public void MarkValidated_應設定ValidatedAt()
    {
        var conn = CreateConnection();
        conn.ValidatedAt.Should().BeNull();

        conn.MarkValidated();

        conn.ValidatedAt.Should().NotBeNull();
        conn.ValidatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    #endregion

    #region UpdateSecret 測試

    [Fact]
    public void UpdateSecret_應替換密鑰並清除ValidatedAt()
    {
        var conn = CreateConnection();
        conn.MarkValidated();
        conn.ValidatedAt.Should().NotBeNull();

        var newSecret = new EncryptedSecret("new-cipher", "key-2");
        conn.UpdateSecret(newSecret);

        conn.EncryptedSecret.Should().Be(newSecret);
        conn.ValidatedAt.Should().BeNull();
    }

    #endregion
}
