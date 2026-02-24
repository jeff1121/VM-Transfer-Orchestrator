using Microsoft.EntityFrameworkCore;
using VMTO.Domain.Aggregates.Artifact;
using VMTO.Domain.Aggregates.Connection;
using VMTO.Domain.Aggregates.License;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Infrastructure.Persistence.Entities;

namespace VMTO.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public DbSet<MigrationJob> Jobs => Set<MigrationJob>();
    public DbSet<JobStep> JobSteps => Set<JobStep>();
    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }
}
