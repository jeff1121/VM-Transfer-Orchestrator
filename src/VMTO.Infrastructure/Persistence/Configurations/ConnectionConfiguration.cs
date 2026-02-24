using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VMTO.Domain.Aggregates.Connection;

namespace VMTO.Infrastructure.Persistence.Configurations;

public sealed class ConnectionConfiguration : IEntityTypeConfiguration<Connection>
{
    public void Configure(EntityTypeBuilder<Connection> builder)
    {
        builder.ToTable("connections");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Name).HasColumnName("name");
        builder.Property(c => c.Type).HasColumnName("type").HasConversion<string>();
        builder.Property(c => c.Endpoint).HasColumnName("endpoint");
        builder.Property(c => c.ValidatedAt).HasColumnName("validated_at");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");

        builder.OwnsOne(c => c.EncryptedSecret, es =>
        {
            es.Property(e => e.CipherText).HasColumnName("cipher_text");
            es.Property(e => e.KeyId).HasColumnName("key_id");
        });
    }
}
