using FluentAssertions;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;
using VMTO.Shared;

namespace VMTO.Domain.Tests;

/// <summary>
/// JobStep 實體的單元測試。
/// 驗證步驟的狀態轉換、重試機制及進度更新。
/// </summary>
public sealed class JobStepTests
{
    private static readonly Guid JobId = Guid.NewGuid();

    // 建立預設的 JobStep 實例
    private static JobStep CreateStep(int maxRetries = 3) => new(JobId, "ExportVmdk", 1, maxRetries);

    // 將步驟推進到指定狀態
    private static JobStep CreateStepInStatus(StepStatus target, int maxRetries = 3)
    {
        var step = CreateStep(maxRetries);
        if (target == StepStatus.Pending) return step;

        step.Start();
        if (target == StepStatus.Running) return step;

        if (target == StepStatus.Succeeded) { step.Complete(); return step; }
        if (target == StepStatus.Failed) { step.Fail("測試錯誤"); return step; }
        if (target == StepStatus.Skipped) { step = CreateStep(maxRetries); step.Skip(); return step; }
        if (target == StepStatus.Retrying) { step.Fail("測試錯誤"); step.Retry(); return step; }

        return step;
    }

    #region 建構子測試

    [Fact]
    public void Constructor_應設定Id且狀態為Pending進度為零()
    {
        var step = CreateStep();

        step.Id.Should().NotBeEmpty();
        step.JobId.Should().Be(JobId);
        step.Name.Should().Be("ExportVmdk");
        step.Order.Should().Be(1);
        step.Status.Should().Be(StepStatus.Pending);
        step.Progress.Should().Be(0);
        step.RetryCount.Should().Be(0);
        step.MaxRetries.Should().Be(3);
        step.ErrorMessage.Should().BeNull();
        step.StartedAt.Should().BeNull();
        step.CompletedAt.Should().BeNull();
    }

    #endregion

    #region Start 測試

    [Fact]
    public void Start_從Pending應成功轉換為Running並設定StartedAt()
    {
        var step = CreateStep();

        var result = step.Start();

        result.IsSuccess.Should().BeTrue();
        step.Status.Should().Be(StepStatus.Running);
        step.StartedAt.Should().NotBeNull();
        step.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Start_從Retrying應成功轉換為Running()
    {
        var step = CreateStepInStatus(StepStatus.Retrying);

        var result = step.Start();

        result.IsSuccess.Should().BeTrue();
        step.Status.Should().Be(StepStatus.Running);
    }

    [Theory]
    [InlineData(StepStatus.Running)]
    [InlineData(StepStatus.Succeeded)]
    [InlineData(StepStatus.Failed)]
    [InlineData(StepStatus.Skipped)]
    public void Start_從不允許的狀態應失敗(StepStatus invalidStatus)
    {
        var step = CreateStepInStatus(invalidStatus);

        var result = step.Start();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    #endregion

    #region Complete 測試

    [Fact]
    public void Complete_從Running應成功且進度為100並設定CompletedAt()
    {
        var step = CreateStepInStatus(StepStatus.Running);

        var result = step.Complete();

        result.IsSuccess.Should().BeTrue();
        step.Status.Should().Be(StepStatus.Succeeded);
        step.Progress.Should().Be(100);
        step.CompletedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(StepStatus.Pending)]
    [InlineData(StepStatus.Succeeded)]
    [InlineData(StepStatus.Failed)]
    [InlineData(StepStatus.Skipped)]
    [InlineData(StepStatus.Retrying)]
    public void Complete_從非Running狀態應失敗(StepStatus invalidStatus)
    {
        var step = CreateStepInStatus(invalidStatus);

        var result = step.Complete();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    #endregion

    #region Fail 測試

    [Fact]
    public void Fail_從Running應成功並記錄錯誤訊息與CompletedAt()
    {
        var step = CreateStepInStatus(StepStatus.Running);

        var result = step.Fail("磁碟 I/O 錯誤");

        result.IsSuccess.Should().BeTrue();
        step.Status.Should().Be(StepStatus.Failed);
        step.ErrorMessage.Should().Be("磁碟 I/O 錯誤");
        step.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_從Retrying應成功()
    {
        var step = CreateStepInStatus(StepStatus.Retrying);

        var result = step.Fail("重試後仍失敗");

        result.IsSuccess.Should().BeTrue();
        step.Status.Should().Be(StepStatus.Failed);
        step.ErrorMessage.Should().Be("重試後仍失敗");
    }

    [Theory]
    [InlineData(StepStatus.Pending)]
    [InlineData(StepStatus.Succeeded)]
    [InlineData(StepStatus.Skipped)]
    public void Fail_從不允許的狀態應失敗(StepStatus invalidStatus)
    {
        var step = CreateStepInStatus(invalidStatus);

        var result = step.Fail("某些錯誤");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    #endregion

    #region Skip 測試

    [Fact]
    public void Skip_從Pending應成功並設定CompletedAt()
    {
        var step = CreateStep();

        var result = step.Skip();

        result.IsSuccess.Should().BeTrue();
        step.Status.Should().Be(StepStatus.Skipped);
        step.CompletedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(StepStatus.Running)]
    [InlineData(StepStatus.Succeeded)]
    [InlineData(StepStatus.Failed)]
    [InlineData(StepStatus.Retrying)]
    public void Skip_從非Pending狀態應失敗(StepStatus invalidStatus)
    {
        var step = CreateStepInStatus(invalidStatus);

        var result = step.Skip();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    #endregion

    #region Retry 測試

    [Fact]
    public void Retry_從Failed應成功且遞增RetryCount並清除ErrorMessage和CompletedAt()
    {
        var step = CreateStepInStatus(StepStatus.Failed);
        step.ErrorMessage.Should().NotBeNull(); // 確認失敗時有錯誤訊息

        var result = step.Retry();

        result.IsSuccess.Should().BeTrue();
        step.Status.Should().Be(StepStatus.Retrying);
        step.RetryCount.Should().Be(1);
        step.ErrorMessage.Should().BeNull();
        step.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Retry_超過MaxRetries應失敗()
    {
        var step = CreateStep(maxRetries: 1);
        step.Start();
        step.Fail("第一次錯誤");
        step.Retry(); // RetryCount = 1, 等於 MaxRetries
        step.Start();
        step.Fail("第二次錯誤");

        // RetryCount(1) >= MaxRetries(1)，應失敗
        var result = step.Retry();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    [Theory]
    [InlineData(StepStatus.Pending)]
    [InlineData(StepStatus.Running)]
    [InlineData(StepStatus.Succeeded)]
    [InlineData(StepStatus.Skipped)]
    [InlineData(StepStatus.Retrying)]
    public void Retry_從非Failed狀態應失敗(StepStatus invalidStatus)
    {
        var step = CreateStepInStatus(invalidStatus);

        var result = step.Retry();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    #endregion

    #region UpdateProgress 測試

    [Fact]
    public void UpdateProgress_從Running應成功設定進度()
    {
        var step = CreateStepInStatus(StepStatus.Running);

        var result = step.UpdateProgress(50);

        result.IsSuccess.Should().BeTrue();
        step.Progress.Should().Be(50);
    }

    [Fact]
    public void UpdateProgress_應將值限制在0到100之間()
    {
        var step = CreateStepInStatus(StepStatus.Running);

        step.UpdateProgress(150);
        step.Progress.Should().Be(100);

        step.UpdateProgress(-10);
        step.Progress.Should().Be(0);
    }

    [Theory]
    [InlineData(StepStatus.Pending)]
    [InlineData(StepStatus.Succeeded)]
    [InlineData(StepStatus.Failed)]
    [InlineData(StepStatus.Skipped)]
    public void UpdateProgress_從非Running狀態應失敗(StepStatus invalidStatus)
    {
        var step = CreateStepInStatus(invalidStatus);

        var result = step.UpdateProgress(50);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    #endregion
}
