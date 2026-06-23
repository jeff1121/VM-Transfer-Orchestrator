using FluentAssertions;
using VMTO.Domain.Aggregates.License;

using License = VMTO.Domain.Aggregates.License.License;

namespace VMTO.Domain.Tests;

/// <summary>
/// License 聚合根的單元測試。
/// 驗證建構、到期判斷及功能查詢。
/// </summary>
public sealed class LicenseTests
{
    private static License CreateLicense(DateTime expiresAt, IEnumerable<string>? features = null)
        => new(
            "LIC-KEY-001",
            LicensePlan.Standard,
            features ?? ["FullCopy", "Incremental", "Scheduling"],
            5,
            expiresAt,
            new Dictionary<string, string> { ["hardware-id"] = "abc-123" },
            "sig-hash-xyz");

    #region 建構子測試

    [Fact]
    public void Constructor_應正確設定所有屬性()
    {
        var expiry = DateTime.UtcNow.AddDays(30);
        var license = CreateLicense(expiry);

        license.Id.Should().NotBeEmpty();
        license.Key.Should().Be("LIC-KEY-001");
        license.Plan.Should().Be(LicensePlan.Standard);
        license.Features.Should().BeEquivalentTo(["FullCopy", "Incremental", "Scheduling"]);
        license.MaxConcurrentJobs.Should().Be(5);
        license.ExpiresAt.Should().Be(expiry);
        license.ActivationBindings.Should().ContainKey("hardware-id");
        license.Signature.Should().Be("sig-hash-xyz");
        license.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    #endregion

    #region IsExpired 測試

    [Fact]
    public void IsExpired_過期時應回傳true()
    {
        var license = CreateLicense(DateTime.UtcNow.AddDays(-1));

        license.IsExpired().Should().BeTrue();
    }

    [Fact]
    public void IsExpired_未過期時應回傳false()
    {
        var license = CreateLicense(DateTime.UtcNow.AddDays(30));

        license.IsExpired().Should().BeFalse();
    }

    #endregion

    #region IsValid 測試

    [Fact]
    public void IsValid_未過期時應回傳true()
    {
        var license = CreateLicense(DateTime.UtcNow.AddDays(30));

        license.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_過期時應回傳false()
    {
        var license = CreateLicense(DateTime.UtcNow.AddDays(-1));

        license.IsValid().Should().BeFalse();
    }

    #endregion

    #region HasFeature 測試

    [Fact]
    public void HasFeature_功能存在時應回傳true()
    {
        var license = CreateLicense(DateTime.UtcNow.AddDays(30));

        license.HasFeature("FullCopy").Should().BeTrue();
    }

    [Fact]
    public void HasFeature_不區分大小寫應回傳true()
    {
        var license = CreateLicense(DateTime.UtcNow.AddDays(30));

        license.HasFeature("fullcopy").Should().BeTrue();
        license.HasFeature("FULLCOPY").Should().BeTrue();
        license.HasFeature("FuLlCoPy").Should().BeTrue();
    }

    [Fact]
    public void HasFeature_功能不存在時應回傳false()
    {
        var license = CreateLicense(DateTime.UtcNow.AddDays(30));

        license.HasFeature("NonExistentFeature").Should().BeFalse();
    }

    #endregion
}
