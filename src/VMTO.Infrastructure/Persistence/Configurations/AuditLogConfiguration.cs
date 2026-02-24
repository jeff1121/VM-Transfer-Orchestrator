using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VMTO.Infrastructure.Persistence.Entities;

namespace VMTO.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.Action).HasColumnName("action");
        builder.Property(a => a.EntityType).HasColumnName("entity_type");
        builder.Property(a => a.EntityId).HasColumnName("entity_id");
        builder.Property(a => a.UserId).HasColumnName("user_id");
        builder.Property(a => a.Details).HasColumnName("details");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
    }
}
