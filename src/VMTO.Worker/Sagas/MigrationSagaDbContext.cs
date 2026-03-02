using Microsoft.EntityFrameworkCore;

namespace VMTO.Worker.Sagas;

public sealed class MigrationSagaDbContext : DbContext
{
    public DbSet<MigrationJobSagaState> MigrationJobSagas => Set<MigrationJobSagaState>();

    public MigrationSagaDbContext(DbContextOptions<MigrationSagaDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MigrationJobSagaState>(entity =>
        {
            entity.ToTable("saga_migration_jobs");
            entity.HasKey(x => x.CorrelationId);
            entity.Property(x => x.CurrentState).HasMaxLength(64).IsRequired();
            entity.Property(x => x.StepNames)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
        });
    }
}
