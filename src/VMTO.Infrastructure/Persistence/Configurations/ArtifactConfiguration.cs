using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VMTO.Domain.Aggregates.Artifact;

namespace VMTO.Infrastructure.Persistence.Configurations;

public sealed class ArtifactConfiguration : IEntityTypeConfiguration<Artifact>
{
    public void Configure(EntityTypeBuilder<Artifact> builder)
    {
        builder.ToTable("artifacts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.JobId).HasColumnName("job_id");
        builder.Property(a => a.FileName).HasColumnName("file_name");
        builder.Property(a => a.Format).HasColumnName("format").HasConversion<string>();
        builder.Property(a => a.SizeBytes).HasColumnName("size_bytes");
        builder.Property(a => a.StorageUri).HasColumnName("storage_uri");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");

        builder.OwnsOne(a => a.Checksum, cs =>
        {
            cs.Property(c => c.Algorithm).HasColumnName("checksum_algorithm");
            cs.Property(c => c.Value).HasColumnName("checksum_value");
        });

        builder.HasIndex(a => a.JobId);
    }
}
