namespace VMTO.Domain.Aggregates.License;

public sealed class License
{
    private readonly List<string> _features;
    private readonly Dictionary<string, string> _activationBindings;

    public Guid Id { get; private set; }
    public string Key { get; private set; }
    public LicensePlan Plan { get; private set; }
    public IReadOnlyList<string> Features => _features.AsReadOnly();
    public int MaxConcurrentJobs { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public IReadOnlyDictionary<string, string> ActivationBindings => _activationBindings.AsReadOnly();
    public string Signature { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public License(
        string key,
        LicensePlan plan,
        IEnumerable<string> features,
        int maxConcurrentJobs,
        DateTime expiresAt,
        IDictionary<string, string> activationBindings,
        string signature)
    {
        Id = Guid.NewGuid();
        Key = key;
        Plan = plan;
        _features = [.. features];
        MaxConcurrentJobs = maxConcurrentJobs;
        ExpiresAt = expiresAt;
        _activationBindings = new Dictionary<string, string>(activationBindings);
        Signature = signature;
        CreatedAt = DateTime.UtcNow;
    }

    // EF Core / serialization
    private License()
    {
        Key = string.Empty;
        _features = [];
        _activationBindings = [];
        Signature = string.Empty;
    }

    public bool IsValid() => !IsExpired();

    public bool HasFeature(string feature) => _features.Contains(feature, StringComparer.OrdinalIgnoreCase);

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;
}
