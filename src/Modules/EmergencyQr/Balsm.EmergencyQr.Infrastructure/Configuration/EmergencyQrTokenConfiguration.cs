using Balsm.EmergencyQr.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.EmergencyQr.Infrastructure.Configuration;

public sealed class EmergencyQrTokenConfiguration : IEntityTypeConfiguration<EmergencyQrToken>
{
    public void Configure(EntityTypeBuilder<EmergencyQrToken> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("jti").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Ciphertext).HasColumnName("ciphertext").HasColumnType("bytea")
            .IsRequired();
        builder.Property(x => x.ProfileEtag).HasColumnName("profile_etag")
            .HasMaxLength(8).IsRequired();
        builder.Property(x => x.TtlSeconds).HasColumnName("ttl_seconds").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Ignore(x => x.IsDeleted);
        builder.Ignore(x => x.DeletedAt);
        builder.Ignore(x => x.DeletedBy);
        builder.Ignore(x => x.UpdatedAt);
        builder.Ignore(x => x.UpdatedBy);
        builder.Ignore(x => x.CreatedBy);

        builder.ToTable("emergency_qr_token", t =>
            t.HasCheckConstraint("CK_emergency_qr_token_ttl",
                "ttl_seconds IN (3600, 21600, 86400, 604800)"));

        // One active token per user
        builder.HasIndex(x => x.UserId)
            .HasFilter("revoked_at IS NULL");
    }
}
