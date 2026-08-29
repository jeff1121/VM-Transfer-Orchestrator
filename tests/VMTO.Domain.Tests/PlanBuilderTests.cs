using FluentAssertions;
using VMTO.Domain.Aggregates.Connection;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;
using VMTO.Domain.Planning;

namespace VMTO.Domain.Tests;

public sealed class PlanBuilderTests
{
    [Fact]
    public void Build_VSphereToPve_FullCopy_EmitsNeutralPerDiskSteps()
    {
        var result = MigrationPlanBuilder.Build(PlatformKind.VSphere, PlatformKind.ProxmoxVE, false, ["disk-0"]);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SourcePlatform.Should().Be(PlatformKind.VSphere);
        result.Value.TargetPlatform.Should().Be(PlatformKind.ProxmoxVE);
        result.Value.Steps.Select(s => s.Kind).Should().Equal(
            MigrationStepKind.ExportDisk,
            MigrationStepKind.ConvertDisk,
            MigrationStepKind.StageArtifact,
            MigrationStepKind.ProvisionTargetVm,
            MigrationStepKind.AttachDisk,
            MigrationStepKind.ConfigureTargetVm,
            MigrationStepKind.VerifyTargetVm,
            MigrationStepKind.Cleanup);
    }

    [Fact]
    public void Build_HyperVToPve_TwoDisks_RepeatsExportConvertStageAndAttach()
    {
        var result = MigrationPlanBuilder.Build(PlatformKind.HyperV, PlatformKind.ProxmoxVE, false, ["disk-0", "disk-1"]);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Steps.Count(s => s.Kind == MigrationStepKind.ExportDisk).Should().Be(2);
        result.Value.Steps.Count(s => s.Kind == MigrationStepKind.AttachDisk).Should().Be(2);
        result.Value.Steps.First(s => s.Kind == MigrationStepKind.ExportDisk).DiskKey.Should().Be("disk-0");
    }

    [Fact]
    public void Build_HyperVIncremental_Fails()
    {
        var result = MigrationPlanBuilder.Build(PlatformKind.HyperV, PlatformKind.ProxmoxVE, true, ["disk-0"]);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(VMTO.Shared.ErrorCodes.Plan.IncrementalNotSupported);
    }

    [Fact]
    public void Build_PveAsSource_Fails()
    {
        var result = MigrationPlanBuilder.Build(PlatformKind.ProxmoxVE, PlatformKind.VSphere, false, ["disk-0"]);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(VMTO.Shared.ErrorCodes.Plan.Incompatible);
    }

    [Fact]
    public void Build_NoDisks_Fails()
    {
        var result = MigrationPlanBuilder.Build(PlatformKind.VSphere, PlatformKind.ProxmoxVE, false, []);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(VMTO.Shared.ErrorCodes.Plan.NoDisks);
    }

    [Fact]
    public void DerivedStrategy_Incremental()
    {
        MigrationPlanBuilder.DerivedStrategy(true).Should().Be(MigrationStrategy.Incremental);
        MigrationPlanBuilder.DerivedStrategy(false).Should().Be(MigrationStrategy.FullCopy);
    }
}
