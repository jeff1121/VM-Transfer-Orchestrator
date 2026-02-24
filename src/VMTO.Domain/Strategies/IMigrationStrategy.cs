namespace VMTO.Domain.Strategies;

public interface IMigrationStrategy
{
    IReadOnlyList<string> GetStepNames();
}
