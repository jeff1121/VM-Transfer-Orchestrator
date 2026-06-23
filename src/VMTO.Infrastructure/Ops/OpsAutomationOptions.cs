namespace VMTO.Infrastructure.Ops;

public sealed class OpsAutomationOptions
{
    public int ArtifactRetentionDays { get; set; } = 7;
    public int StuckJobThresholdMinutes { get; set; } = 20;
    public int FailedJobRetryDelayMinutes { get; set; } = 5;
    public long StorageUsageThresholdBytes { get; set; } = 50L * 1024 * 1024 * 1024;
    public string BackupBucketName { get; set; } = "backups";
    public int DatabaseBackupTimeoutSeconds { get; set; } = 300;
}
