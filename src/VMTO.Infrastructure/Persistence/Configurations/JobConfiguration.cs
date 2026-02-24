using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VMTO.Domain.Aggregates.MigrationJob;

namespace VMTO.Infrastructure.Persistence.Configurations;

public sealed class JobConfiguration : IEntityTypeConfiguration<MigrationJob>
{
    public void Configure(EntityTypeBuilder<MigrationJob> builder)
    {
        builder.ToTable("jobs");

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasColumnName("id");

        builder.Property(j => j.CorrelationId)
            .HasColumnName("correlation_id")
            .HasConversion(c => c.Value, v => new Shared.CorrelationId(v));

        builder.Property(j => j.SourceConnectionId).HasColumnName("source_connection_id");
        builder.Property(j => j.TargetConnectionId).HasColumnName("target_connection_id");

        builder.OwnsOne(j => j.StorageTarget, st =>
        {
            st.Property(s => s.Type).HasColumnName("storage_type").HasConversion<string>();
            st.Property(s => s.Endpoint).HasColumnName("storage_endpoint");
            st.Property(s => s.BucketOrPath).HasColumnName("storage_bucket_or_path");
            st.Property(s => s.Region).HasColumnName("storage_region");
        });

        builder.Property(j => j.Strategy).HasColumnName("strategy").HasConversion<string>();
        builder.Property(j => j.Status).HasColumnName("status").HasConversion<string>();
        builder.Property(j => j.Progress).HasColumnName("progress");
        builder.Property(j => j.Result).HasColumnName("result");

        builder.Property(j => j.Options)
            .HasColumnName("options")
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<MigrationOptions>(v, (System.Text.Json.JsonSerializerOptions?)null)!);

        builder.Property(j => j.CreatedAt).HasColumnName("created_at");
        builder.Property(j => j.UpdatedAt).HasColumnName("updated_at");

        builder.HasMany(j => j.Steps)
            .WithOne()
            .HasForeignKey(s => s.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(MigrationJob.Steps))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(j => j.DomainEvents);
    }
}
