using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VMTO.Domain.Aggregates.License;

namespace VMTO.Infrastructure.Persistence.Configurations;

public sealed class LicenseConfiguration : IEntityTypeConfiguration<License>
{
    public void Configure(EntityTypeBuilder<License> builder)
    {
        builder.ToTable("licenses");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.Key).HasColumnName("key");
        builder.Property(l => l.Plan).HasColumnName("plan").HasConversion<string>();
        builder.Property(l => l.MaxConcurrentJobs).HasColumnName("max_concurrent_jobs");
        builder.Property(l => l.ExpiresAt).HasColumnName("expires_at");
        builder.Property(l => l.Signature).HasColumnName("signature");
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");

        builder.Property(l => l.Features)
            .HasColumnName("features")
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>());

        builder.Property(l => l.ActivationBindings)
            .HasColumnName("activation_bindings")
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, string>());

        builder.HasIndex(l => l.Key).IsUnique();
    }
}
