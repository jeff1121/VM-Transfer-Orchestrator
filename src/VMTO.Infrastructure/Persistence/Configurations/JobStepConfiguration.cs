using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VMTO.Domain.Aggregates.MigrationJob;

namespace VMTO.Infrastructure.Persistence.Configurations;

public sealed class JobStepConfiguration : IEntityTypeConfiguration<JobStep>
{
    public void Configure(EntityTypeBuilder<JobStep> builder)
    {
        builder.ToTable("job_steps");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.JobId).HasColumnName("job_id");
        builder.Property(s => s.Name).HasColumnName("name");
        builder.Property(s => s.Order).HasColumnName("order");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>();
        builder.Property(s => s.Progress).HasColumnName("progress");
        builder.Property(s => s.RetryCount).HasColumnName("retry_count");
        builder.Property(s => s.MaxRetries).HasColumnName("max_retries");
        builder.Property(s => s.ErrorMessage).HasColumnName("error_message");
        builder.Property(s => s.LogsUri).HasColumnName("logs_uri");
        builder.Property(s => s.StartedAt).HasColumnName("started_at");
        builder.Property(s => s.CompletedAt).HasColumnName("completed_at");
    }
}
