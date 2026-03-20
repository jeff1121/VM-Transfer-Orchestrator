namespace VMTO.Shared;

public readonly record struct CorrelationId(string Value)
{
    public static CorrelationId New() => new(Guid.NewGuid().ToString("N"));
    public static CorrelationId From(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        return new(value);
    }
    public override string ToString() => Value;
}
