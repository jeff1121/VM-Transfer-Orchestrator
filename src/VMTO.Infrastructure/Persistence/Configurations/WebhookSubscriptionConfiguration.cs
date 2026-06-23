using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VMTO.Infrastructure.Persistence.Entities;

namespace VMTO.Infrastructure.Persistence.Configurations;

/// <summary>WebhookSubscription 實體的 EF Core 設定</summary>
public sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("webhook_subscriptions");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasColumnName("id");
        builder.Property(w => w.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(w => w.Type).HasColumnName("type").HasMaxLength(50).IsRequired();
        builder.Property(w => w.Target).HasColumnName("target").HasMaxLength(500).IsRequired();
        builder.Property(w => w.Events).HasColumnName("events").HasMaxLength(500).IsRequired();
        builder.Property(w => w.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(true);
        builder.Property(w => w.CustomHeaders).HasColumnName("custom_headers").HasMaxLength(2000);
        builder.Property(w => w.Secret).HasColumnName("secret").HasMaxLength(500);
        builder.Property(w => w.CreatedAt).HasColumnName("created_at");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");
    }
}
