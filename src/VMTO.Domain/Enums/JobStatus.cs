namespace VMTO.Domain.Enums;

public enum JobStatus
{
    Created,
    Queued,
    Running,
    Pausing,
    Paused,
    Resuming,
    Cancelling,
    Cancelled,
    Failed,
    Succeeded
}
