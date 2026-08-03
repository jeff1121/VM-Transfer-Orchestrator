using FluentAssertions;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;
using VMTO.Domain.Strategies;

namespace VMTO.Domain.Tests;

/// <summary>
/// 遷移策略的單元測試。
/// 驗證 FullCopyStrategy、IncrementalStrategy 與 HyperVOfflineExportStrategy 的強型別 Plan 及介面實作。
/// </summary>
public sealed class StrategyTests
{
    #region FullCopyStrategy 測試

    [Fact]
    public void FullCopyStrategy_GetPlan_應回傳正確的Plan與步驟清單()
    {
        var strategy = new FullCopyStrategy();

        var plan = strategy.GetPlan();

        plan.Strategy.Should().Be(MigrationStrategy.FullCopy);
        plan.Steps.Should().Equal(
            MigrationStepType.ExportVmdk,
            MigrationStepType.ConvertDisk,
            MigrationStepType.UploadArtifact,
            MigrationStepType.ImportToPve,
            MigrationStepType.Verify);
    }

    [Fact]
    public void FullCopyStrategy_應實作IMigrationStrategy()
    {
        var strategy = new FullCopyStrategy();

        strategy.Should().BeAssignableTo<IMigrationStrategy>();
    }

    #endregion

    #region IncrementalStrategy 測試

    [Fact]
    public void IncrementalStrategy_GetPlan_應回傳正確的Plan與步驟清單()
    {
        var strategy = new IncrementalStrategy();

        var plan = strategy.GetPlan();

        plan.Strategy.Should().Be(MigrationStrategy.Incremental);
        plan.Steps.Should().Equal(
            MigrationStepType.EnableCbt,
            MigrationStepType.IncrementalPull,
            MigrationStepType.ApplyDelta,
            MigrationStepType.FinalSyncCutover,
            MigrationStepType.Verify);
    }

    [Fact]
    public void IncrementalStrategy_應實作IMigrationStrategy()
    {
        var strategy = new IncrementalStrategy();

        strategy.Should().BeAssignableTo<IMigrationStrategy>();
    }

    #endregion

    #region HyperVOfflineExportStrategy 測試

    [Fact]
    public void HyperVOfflineExportStrategy_GetPlan_應回傳正確的Plan與步驟清單()
    {
        var strategy = new HyperVOfflineExportStrategy();

        var plan = strategy.GetPlan();

        plan.Strategy.Should().Be(MigrationStrategy.HyperVOffline);
        plan.Steps.Should().Equal(
            MigrationStepType.ExportVhdx,
            MigrationStepType.ConvertDisk,
            MigrationStepType.UploadArtifact,
            MigrationStepType.ImportToPve,
            MigrationStepType.Verify);
    }

    [Fact]
    public void HyperVOfflineExportStrategy_應實作IMigrationStrategy()
    {
        var strategy = new HyperVOfflineExportStrategy();

        strategy.Should().BeAssignableTo<IMigrationStrategy>();
    }

    #endregion
}
