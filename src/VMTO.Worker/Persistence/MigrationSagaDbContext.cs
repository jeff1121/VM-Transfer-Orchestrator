using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VMTO.Worker.Sagas;

namespace VMTO.Worker.Persistence;

public sealed class MigrationSagaDbContext : SagaDbContext
{
    public MigrationSagaDbContext(DbContextOptions<MigrationSagaDbContext> options)
        : base(options) { }

    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get { yield return new MigrationJobSagaStateMap(); }
    }
}

public sealed class MigrationJobSagaStateMap : SagaClassMap<MigrationJobSagaState>
{
    protected override void Configure(EntityTypeBuilder<MigrationJobSagaState> entity, ModelBuilder model)
    {
        entity.ToTable("migration_saga_states");
        entity.Property(x => x.CurrentState).HasMaxLength(64);
        entity.Property(x => x.VmId).HasMaxLength(256);
        entity.Property(x => x.DiskKey).HasMaxLength(256);
        entity.Property(x => x.TargetFormat).HasMaxLength(32);
        entity.Property(x => x.VmName).HasMaxLength(256);
        entity.Property(x => x.StepNames).HasColumnType("jsonb");
        entity.Property(x => x.StepIds).HasColumnType("jsonb");
        entity.Property(x => x.StepOutputData).HasColumnType("jsonb");
    }
}
