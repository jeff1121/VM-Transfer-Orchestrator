namespace VMTO.Shared;

public readonly record struct CorrelationId(string Value)
{
    public static CorrelationId New() => new(Guid.NewGuid().ToString("N"));
    public static CorrelationId From(string value) => new(value);
    public override string ToString() => Value;
}
