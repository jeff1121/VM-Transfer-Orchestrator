using VMTO.Domain.ValueObjects;

namespace VMTO.Domain.Strategies;

public interface IMigrationStrategy
{
    MigrationPlan GetPlan();
}
