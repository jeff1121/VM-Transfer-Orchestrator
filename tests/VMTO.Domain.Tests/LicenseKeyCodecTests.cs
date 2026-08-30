using FluentAssertions;
using VMTO.Domain.Aggregates.License;
using VMTO.Domain.Licensing;

namespace VMTO.Domain.Tests;

public sealed class LicenseKeyCodecTests
{
    [Fact]
    public void GenerateAndDecode_ValidKey_ShouldMatchAllProperties()
    {
        var plan = LicensePlan.Enterprise;
        var maxJobs = 15;
        var expiry = new DateTime(2028, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var features = new[] { "vsphere", "hyperv", "incremental-sync", "api-access" };

        var key = LicenseKeyCodec.Generate(plan, maxJobs, expiry, features);

        key.Should().NotBeNullOrWhiteSpace();
        key.Length.Should().Be(19); // 16 chars + 3 hyphens (XXXX-XXXX-XXXX-XXXX)

        var decoded = LicenseKeyCodec.DecodeAndValidate(key);
        decoded.IsSuccess.Should().BeTrue();

        var d = decoded.Value!;
        d.Plan.Should().Be(plan);
        d.MaxConcurrentJobs.Should().Be(maxJobs);
        d.ExpiresAt.Year.Should().Be(2028);
        d.ExpiresAt.Month.Should().Be(6);
        d.Features.Should().Contain(["vsphere", "hyperv", "incremental-sync", "api-access"]);
    }

    [Fact]
    public void DecodeAndValidate_TamperedKey_ShouldFailSignature()
    {
        var key = LicenseKeyCodec.Generate(LicensePlan.Standard, 5, DateTime.UtcNow.AddYears(1), ["vsphere"]);
        var chars = key.ToCharArray();
        chars[0] = chars[0] == 'A' ? 'B' : 'A';
        var tampered = new string(chars);

        var decoded = LicenseKeyCodec.DecodeAndValidate(tampered);
        decoded.IsSuccess.Should().BeFalse();
        decoded.ErrorCode.Should().Be("LIC_INVALID");
    }

    [Fact]
    public void DecodeAndValidate_ExpiredKey_ShouldFailWithExpiredCode()
    {
        var key = LicenseKeyCodec.Generate(LicensePlan.Trial, 2, new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc), ["vsphere"]);
        var decoded = LicenseKeyCodec.DecodeAndValidate(key);

        decoded.IsSuccess.Should().BeFalse();
        decoded.ErrorCode.Should().Be("LIC_EXPIRED");
    }

    [Fact]
    public void GenerateSampleKeys_ForTesting()
    {
        // 1. Enterprise (All features, 20 concurrent jobs, expires 2028-12-31)
        var entKey = LicenseKeyCodec.Generate(
            LicensePlan.Enterprise,
            20,
            new DateTime(2028, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            ["vsphere", "hyperv", "incremental-sync", "api-access"]);

        // 2. Standard (vSphere + Hyper-V, 5 concurrent jobs, expires 2027-12-31)
        var stdKey = LicenseKeyCodec.Generate(
            LicensePlan.Standard,
            5,
            new DateTime(2027, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            ["vsphere", "hyperv"]);

        // 3. Trial (vSphere only, 2 concurrent jobs, expires 2026-12-31)
        var trialKey = LicenseKeyCodec.Generate(
            LicensePlan.Trial,
            2,
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            ["vsphere"]);

        System.Console.WriteLine($"[KEY_SAMPLE_ENTERPRISE]: {entKey}");
        System.Console.WriteLine($"[KEY_SAMPLE_STANDARD]: {stdKey}");
        System.Console.WriteLine($"[KEY_SAMPLE_TRIAL]: {trialKey}");
    }
}
