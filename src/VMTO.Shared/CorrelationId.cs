namespace VMTO.Shared;

public readonly record struct CorrelationId(string Value)
{
    public static CorrelationId New() => new(Guid.NewGuid().ToString("N"));
<<<<<<< HEAD

    public static CorrelationId From(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value, nameof(value));
        return new(value);
    }

=======
    public static CorrelationId From(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        return new(value);
    }
>>>>>>> origin/main
    public override string ToString() => Value;
}
