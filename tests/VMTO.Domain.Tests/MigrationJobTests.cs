using FluentAssertions;
using VMTO.Domain.Aggregates.Artifact;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;
using VMTO.Domain.Events;
using VMTO.Domain.ValueObjects;
using VMTO.Shared;

namespace VMTO.Domain.Tests;

/// <summary>
/// MigrationJob 聚合根的單元測試。
/// 驗證建構、狀態機轉換、事件發布及進度計算等行為。
/// </summary>
public sealed class MigrationJobTests
{
    private static readonly Guid SourceConnId = Guid.NewGuid();
    private static readonly Guid TargetConnId = Guid.NewGuid();
    private static readonly StorageTarget DefaultStorage = new(StorageType.S3, "http://localhost:9000", "bucket", "us-east-1");
    private static readonly MigrationOptions DefaultOptions = new(ArtifactFormat.Qcow2, false, true, 3);

    // 建立預設的 MigrationJob 實例
    private static MigrationJob CreateJob(CorrelationId? correlationId = null)
        => new(SourceConnId, TargetConnId, DefaultStorage, MigrationStrategy.FullCopy, DefaultOptions, correlationId);

    // 將 Job 推進到指定狀態
    private static MigrationJob CreateJobInStatus(JobStatus target)
    {
        var job = CreateJob();
        if (target == JobStatus.Created) return job;

        job.Enqueue();
        if (target == JobStatus.Queued) return job;

        job.Start();
        if (target == JobStatus.Running) return job;

        if (target == JobStatus.Pausing) { job.RequestPause(); return job; }
        if (target == JobStatus.Paused) { job.RequestPause(); job.Pause(); return job; }
        if (target == JobStatus.Resuming) { job.RequestPause(); job.Pause(); job.RequestResume(); return job; }

        if (target == JobStatus.Cancelling) { job.RequestCancel(); return job; }
        if (target == JobStatus.Cancelled) { job.RequestCancel(); job.Cancel(); return job; }

        if (target == JobStatus.Failed) { job.Fail("error"); return job; }

        if (target == JobStatus.Succeeded)
        {
            // 加一個已完成的步驟，才能成功 Complete
            job.AddStep("Test", 1);
            job.Steps[0].Start();
            job.Steps[0].Complete();
            job.Complete();
            return job;
        }

        return job;
    }

    #region 建構子測試

    [Fact]
    public void Constructor_應建立Created狀態的Job()
    {
        var job = CreateJob();

        job.Id.Should().NotBeEmpty();
        job.Status.Should().Be(JobStatus.Created);
        job.SourceConnectionId.Should().Be(SourceConnId);
        job.TargetConnectionId.Should().Be(TargetConnId);
        job.StorageTarget.Should().Be(DefaultStorage);
        job.Strategy.Should().Be(MigrationStrategy.FullCopy);
        job.Options.Should().Be(DefaultOptions);
        job.Progress.Should().Be(0);
        job.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Constructor_應發出JobCreatedEvent()
    {
        var job = CreateJob();

        job.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<JobCreatedEvent>()
            .Which.JobId.Should().Be(job.Id);
    }

    [Fact]
    public void Constructor_使用提供的CorrelationId()
    {
        var correlationId = CorrelationId.From("test-correlation");
        var job = CreateJob(correlationId);

        job.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void Constructor_未提供CorrelationId時自動產生()
    {
        var job = CreateJob();

        job.CorrelationId.Value.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Enqueue 測試

    [Fact]
    public void Enqueue_從Created應成功轉換為Queued()
    {
        var job = CreateJob();
        job.ClearDomainEvents();

        var result = job.Enqueue();

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Queued);
    }

    [Theory]
    [InlineData(JobStatus.Queued)]
    [InlineData(JobStatus.Running)]
    [InlineData(JobStatus.Paused)]
    [InlineData(JobStatus.Cancelled)]
    [InlineData(JobStatus.Failed)]
    [InlineData(JobStatus.Succeeded)]
    public void Enqueue_從非Created狀態應失敗(JobStatus invalidStatus)
    {
        var job = CreateJobInStatus(invalidStatus);

        var result = job.Enqueue();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    #endregion

    #region Start 測試

    [Fact]
    public void Start_從Queued應成功轉換為Running()
    {
        var job = CreateJobInStatus(JobStatus.Queued);
        job.ClearDomainEvents();

        var result = job.Start();

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Running);
    }

    [Theory]
    [InlineData(JobStatus.Created)]
    [InlineData(JobStatus.Running)]
    [InlineData(JobStatus.Paused)]
    [InlineData(JobStatus.Cancelled)]
    public void Start_從非Queued狀態應失敗(JobStatus invalidStatus)
    {
        var job = CreateJobInStatus(invalidStatus);

        var result = job.Start();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    #endregion

    #region RequestPause / Pause 測試

    [Fact]
    public void RequestPause_從Running應成功轉換為Pausing()
    {
        var job = CreateJobInStatus(JobStatus.Running);
        job.ClearDomainEvents();

        var result = job.RequestPause();

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Pausing);
    }

    [Theory]
    [InlineData(JobStatus.Created)]
    [InlineData(JobStatus.Queued)]
    [InlineData(JobStatus.Paused)]
    [InlineData(JobStatus.Cancelled)]
    public void RequestPause_從非Running狀態應失敗(JobStatus invalidStatus)
    {
        var job = CreateJobInStatus(invalidStatus);

        var result = job.RequestPause();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    [Fact]
    public void Pause_從Pausing應成功轉換為Paused()
    {
        var job = CreateJobInStatus(JobStatus.Pausing);
        job.ClearDomainEvents();

        var result = job.Pause();

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Paused);
    }

    [Theory]
    [InlineData(JobStatus.Created)]
    [InlineData(JobStatus.Running)]
    [InlineData(JobStatus.Paused)]
    public void Pause_從非Pausing狀態應失敗(JobStatus invalidStatus)
    {
        var job = CreateJobInStatus(invalidStatus);

        var result = job.Pause();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    #endregion

    #region RequestResume / Resume 測試

    [Fact]
    public void RequestResume_從Paused應成功轉換為Resuming()
    {
        var job = CreateJobInStatus(JobStatus.Paused);
        job.ClearDomainEvents();

        var result = job.RequestResume();

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Resuming);
    }

    [Theory]
    [InlineData(JobStatus.Created)]
    [InlineData(JobStatus.Running)]
    [InlineData(JobStatus.Cancelled)]
    public void RequestResume_從非Paused狀態應失敗(JobStatus invalidStatus)
    {
        var job = CreateJobInStatus(invalidStatus);

        var result = job.RequestResume();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    [Fact]
    public void Resume_從Resuming應成功轉換為Running()
    {
        var job = CreateJobInStatus(JobStatus.Resuming);
        job.ClearDomainEvents();

        var result = job.Resume();

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Running);
    }

    [Theory]
    [InlineData(JobStatus.Created)]
    [InlineData(JobStatus.Running)]
    [InlineData(JobStatus.Paused)]
    public void Resume_從非Resuming狀態應失敗(JobStatus invalidStatus)
    {
        var job = CreateJobInStatus(invalidStatus);

        var result = job.Resume();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    #endregion

    #region RequestCancel / Cancel 測試

    [Theory]
    [InlineData(JobStatus.Running)]
    [InlineData(JobStatus.Pausing)]
    [InlineData(JobStatus.Paused)]
    [InlineData(JobStatus.Queued)]
    public void RequestCancel_從允許的狀態應成功轉換為Cancelling(JobStatus validStatus)
    {
        var job = CreateJobInStatus(validStatus);
        job.ClearDomainEvents();

        var result = job.RequestCancel();

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Cancelling);
    }

    [Theory]
    [InlineData(JobStatus.Created)]
    [InlineData(JobStatus.Cancelled)]
    [InlineData(JobStatus.Failed)]
    [InlineData(JobStatus.Succeeded)]
    public void RequestCancel_從不允許的狀態應失敗(JobStatus invalidStatus)
    {
        var job = CreateJobInStatus(invalidStatus);

        var result = job.RequestCancel();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    [Fact]
    public void Cancel_從Cancelling應成功轉換為Cancelled()
    {
        var job = CreateJobInStatus(JobStatus.Cancelling);
        job.ClearDomainEvents();

        var result = job.Cancel();

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Cancelled);
    }

    [Theory]
    [InlineData(JobStatus.Created)]
    [InlineData(JobStatus.Running)]
    [InlineData(JobStatus.Paused)]
    public void Cancel_從非Cancelling狀態應失敗(JobStatus invalidStatus)
    {
        var job = CreateJobInStatus(invalidStatus);

        var result = job.Cancel();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    #endregion

    #region Fail 測試

    [Fact]
    public void Fail_從Running應成功轉換為Failed並記錄原因()
    {
        var job = CreateJobInStatus(JobStatus.Running);
        job.ClearDomainEvents();

        var result = job.Fail("磁碟空間不足");

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Failed);
        job.Result.Should().Be("磁碟空間不足");
    }

    [Fact]
    public void Fail_從Resuming應成功轉換為Failed()
    {
        var job = CreateJobInStatus(JobStatus.Resuming);
        job.ClearDomainEvents();

        var result = job.Fail("網路中斷");

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Failed);
        job.Result.Should().Be("網路中斷");
    }

    [Theory]
    [InlineData(JobStatus.Created)]
    [InlineData(JobStatus.Queued)]
    [InlineData(JobStatus.Paused)]
    [InlineData(JobStatus.Cancelled)]
    [InlineData(JobStatus.Succeeded)]
    public void Fail_從不允許的狀態應失敗(JobStatus invalidStatus)
    {
        var job = CreateJobInStatus(invalidStatus);

        var result = job.Fail("某些錯誤");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    #endregion

    #region Complete 測試

    [Fact]
    public void Complete_所有步驟已完成應成功轉換為Succeeded()
    {
        var job = CreateJobInStatus(JobStatus.Running);
        job.AddStep("ExportVmdk", 1);
        job.AddStep("ConvertDisk", 2);
        // 將所有步驟設為已完成
        foreach (var step in job.Steps)
        {
            step.Start();
            step.Complete();
        }
        job.ClearDomainEvents();

        var result = job.Complete();

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Succeeded);
    }

    [Fact]
    public void Complete_包含Skipped步驟也應成功()
    {
        var job = CreateJobInStatus(JobStatus.Running);
        job.AddStep("ExportVmdk", 1);
        job.AddStep("ConvertDisk", 2);
        job.Steps[0].Start();
        job.Steps[0].Complete();
        job.Steps[1].Skip();
        job.ClearDomainEvents();

        var result = job.Complete();

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Succeeded);
    }

    [Fact]
    public void Complete_有未完成步驟應失敗()
    {
        var job = CreateJobInStatus(JobStatus.Running);
        job.AddStep("ExportVmdk", 1);
        job.AddStep("ConvertDisk", 2);
        job.Steps[0].Start();
        job.Steps[0].Complete();
        // 步驟2 仍為 Pending

        var result = job.Complete();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    [Theory]
    [InlineData(JobStatus.Created)]
    [InlineData(JobStatus.Queued)]
    [InlineData(JobStatus.Paused)]
    [InlineData(JobStatus.Cancelled)]
    [InlineData(JobStatus.Failed)]
    public void Complete_從非Running狀態應失敗(JobStatus invalidStatus)
    {
        var job = CreateJobInStatus(invalidStatus);

        var result = job.Complete();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Job.InvalidTransition);
    }

    #endregion

    #region AddStep 測試

    [Fact]
    public void AddStep_應新增步驟到列表()
    {
        var job = CreateJob();

        job.AddStep("ExportVmdk", 1);
        job.AddStep("ConvertDisk", 2);

        job.Steps.Should().HaveCount(2);
        job.Steps[0].Name.Should().Be("ExportVmdk");
        job.Steps[0].Order.Should().Be(1);
        job.Steps[1].Name.Should().Be("ConvertDisk");
        job.Steps[1].Order.Should().Be(2);
    }

    #endregion

    #region UpdateProgress 測試

    [Fact]
    public void UpdateProgress_應計算步驟進度平均值()
    {
        var job = CreateJobInStatus(JobStatus.Running);
        job.AddStep("Step1", 1);
        job.AddStep("Step2", 2);
        job.Steps[0].Start();
        job.Steps[0].UpdateProgress(80);
        job.Steps[1].Start();
        job.Steps[1].UpdateProgress(40);

        job.UpdateProgress();

        job.Progress.Should().Be(60); // (80 + 40) / 2
    }

    [Fact]
    public void UpdateProgress_無步驟時進度為零()
    {
        var job = CreateJob();

        job.UpdateProgress();

        job.Progress.Should().Be(0);
    }

    #endregion

    #region ClearDomainEvents 測試

    [Fact]
    public void ClearDomainEvents_應清除所有事件()
    {
        var job = CreateJob();
        job.DomainEvents.Should().NotBeEmpty();

        job.ClearDomainEvents();

        job.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region 事件發布測試

    [Fact]
    public void 有效的狀態轉換應發出JobStatusChangedEvent()
    {
        var job = CreateJob();
        job.ClearDomainEvents();

        job.Enqueue();

        job.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<JobStatusChangedEvent>()
            .Which.Should().Match<JobStatusChangedEvent>(e =>
                e.OldStatus == JobStatus.Created && e.NewStatus == JobStatus.Queued);
    }

    [Fact]
    public void 無效的狀態轉換不應發出事件()
    {
        var job = CreateJob();
        job.ClearDomainEvents();

        job.Start(); // 從 Created 直接 Start 應失敗

        job.DomainEvents.Should().BeEmpty();
    }

    #endregion
}
