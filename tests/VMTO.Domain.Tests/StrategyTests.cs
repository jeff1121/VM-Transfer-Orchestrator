using FluentAssertions;
using VMTO.Domain.Strategies;

namespace VMTO.Domain.Tests;

/// <summary>
/// 遷移策略的單元測試。
/// 驗證 FullCopyStrategy 和 IncrementalStrategy 的步驟名稱及介面實作。
/// </summary>
public sealed class StrategyTests
{
    #region FullCopyStrategy 測試

    [Fact]
    public void FullCopyStrategy_GetStepNames_應回傳正確的步驟清單()
    {
        var strategy = new FullCopyStrategy();

        var steps = strategy.GetStepNames();

        steps.Should().Equal("ExportVmdk", "ConvertDisk", "UploadArtifact", "ImportToPve", "Verify");
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
    public void IncrementalStrategy_GetStepNames_應回傳正確的步驟清單()
    {
        var strategy = new IncrementalStrategy();

        var steps = strategy.GetStepNames();

        steps.Should().Equal("EnableCbt", "IncrementalPull", "ApplyDelta", "FinalSyncCutover", "Verify");
    }

    [Fact]
    public void IncrementalStrategy_應實作IMigrationStrategy()
    {
        var strategy = new IncrementalStrategy();

        strategy.Should().BeAssignableTo<IMigrationStrategy>();
    }

    #endregion
}
