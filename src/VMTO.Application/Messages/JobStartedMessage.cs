using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;

namespace VMTO.Application.Messages;

public sealed record JobStartedMessage(
    Guid JobId,
    List<string> StepNames,
    List<Guid> StepIds,
    Guid SourceConnectionId,
    Guid TargetConnectionId,
    string StorageEndpoint,
    string StorageBucket,
    Guid CorrelationId,
    string VmId = "",
    string DiskKey = "",
    string TargetFormat = "qcow2",
    string VmName = "",
    int Cores = 2,
    int MemoryMb = 2048,
    List<MigrationStepKind>? StepTypes = null,
    string SourcePlatform = "",
    string TargetPlatform = "",
    List<string>? StepDiskKeys = null)
{
    public static JobStartedMessage FromJob(MigrationJob job)
    {
        var plan = job.Plan;
        var diskKey = plan?.Steps.Select(s => s.DiskKey).FirstOrDefault(k => !string.IsNullOrEmpty(k)) ?? "disk-0";
        return new JobStartedMessage(
            job.Id,
            job.Steps.Select(s => s.Name).ToList(),
            job.Steps.Select(s => s.Id).ToList(),
            job.SourceConnectionId,
            job.TargetConnectionId,
            job.StorageTarget.Endpoint,
            job.StorageTarget.BucketOrPath,
            Guid.TryParse(job.CorrelationId.Value, out var correlation) ? correlation : job.Id,
            job.VmId,
            diskKey,
            job.Options.TargetDiskFormat.ToString().ToLowerInvariant(),
            string.IsNullOrEmpty(job.VmId) ? job.Id.ToString("N")[..8] : job.VmId,
            2,
            2048,
            job.Steps.Select(s => s.StepType).ToList(),
            plan?.SourcePlatform.ToString() ?? string.Empty,
            plan?.TargetPlatform.ToString() ?? string.Empty,
            plan?.Steps.Select(s => s.DiskKey).ToList());
    }
}
