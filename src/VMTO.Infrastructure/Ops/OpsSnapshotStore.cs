namespace VMTO.Infrastructure.Ops;

public sealed record HealthSnapshot(
    DateTime GeneratedAt,
    bool DatabaseReachable,
    int TotalJobs,
    int RunningJobs,
    int FailedJobs,
    int QueuedJobs);

public sealed record StorageUsageSnapshot(
    DateTime GeneratedAt,
    string Bucket,
    long TotalBytes,
    int ObjectCount,
    bool ThresholdExceeded);

public interface IOpsSnapshotStore
{
    HealthSnapshot? GetLatestHealthSnapshot();
    StorageUsageSnapshot? GetLatestStorageSnapshot();
    IReadOnlyList<HealthSnapshot> GetHealthSnapshots(int take = 20);
    IReadOnlyList<StorageUsageSnapshot> GetStorageSnapshots(int take = 20);
    void AddHealthSnapshot(HealthSnapshot snapshot);
    void AddStorageSnapshot(StorageUsageSnapshot snapshot);
}

public sealed class OpsSnapshotStore : IOpsSnapshotStore
{
    private readonly object _sync = new();
    private readonly List<HealthSnapshot> _healthSnapshots = [];
    private readonly List<StorageUsageSnapshot> _storageSnapshots = [];
    private const int MaxItems = 200;

    public HealthSnapshot? GetLatestHealthSnapshot()
    {
        lock (_sync)
        {
            return _healthSnapshots.FirstOrDefault();
        }
    }

    public StorageUsageSnapshot? GetLatestStorageSnapshot()
    {
        lock (_sync)
        {
            return _storageSnapshots.FirstOrDefault();
        }
    }

    public IReadOnlyList<HealthSnapshot> GetHealthSnapshots(int take = 20)
    {
        lock (_sync)
        {
            return _healthSnapshots.Take(Math.Max(1, take)).ToArray();
        }
    }

    public IReadOnlyList<StorageUsageSnapshot> GetStorageSnapshots(int take = 20)
    {
        lock (_sync)
        {
            return _storageSnapshots.Take(Math.Max(1, take)).ToArray();
        }
    }

    public void AddHealthSnapshot(HealthSnapshot snapshot)
    {
        lock (_sync)
        {
            _healthSnapshots.Insert(0, snapshot);
            Trim(_healthSnapshots);
        }
    }

    public void AddStorageSnapshot(StorageUsageSnapshot snapshot)
    {
        lock (_sync)
        {
            _storageSnapshots.Insert(0, snapshot);
            Trim(_storageSnapshots);
        }
    }

    private static void Trim<T>(List<T> list)
    {
        if (list.Count > MaxItems)
        {
            list.RemoveRange(MaxItems, list.Count - MaxItems);
        }
    }
}
