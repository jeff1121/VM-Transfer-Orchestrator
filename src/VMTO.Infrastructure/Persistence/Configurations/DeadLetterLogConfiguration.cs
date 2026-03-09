using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VMTO.Infrastructure.Persistence.Entities;

namespace VMTO.Infrastructure.Persistence.Configurations;

public sealed class DeadLetterLogConfiguration : IEntityTypeConfiguration<DeadLetterLogEntry>
{
    public void Configure(EntityTypeBuilder<DeadLetterLogEntry> builder)
    {
        builder.ToTable("dead_letter_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MessageType).HasColumnName("message_type").IsRequired();
        builder.Property(x => x.QueueName).HasColumnName("queue_name").IsRequired();
        builder.Property(x => x.Payload).HasColumnName("payload").IsRequired();
        builder.Property(x => x.Error).HasColumnName("error");
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.ReplayedAt).HasColumnName("replayed_at");

        builder.HasIndex(x => x.Status).HasDatabaseName("ix_dead_letter_logs_status");
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_dead_letter_logs_created_at");
    }
}
