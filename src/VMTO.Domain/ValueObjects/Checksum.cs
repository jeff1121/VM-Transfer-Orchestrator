namespace VMTO.Domain.ValueObjects;

public sealed record Checksum
{
    public string Algorithm { get; }
    public string Value { get; }

    public Checksum(string algorithm, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(algorithm, nameof(algorithm));
        ArgumentException.ThrowIfNullOrEmpty(value, nameof(value));
        Algorithm = algorithm;
        Value = value;
    }
}
