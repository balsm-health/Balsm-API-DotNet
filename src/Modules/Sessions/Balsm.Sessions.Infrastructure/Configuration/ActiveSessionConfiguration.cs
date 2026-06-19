using Balsm.Sessions.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Sessions.Infrastructure.Configuration;

public sealed class ActiveSessionConfiguration : IEntityTypeConfiguration<ActiveSession>
{
    public void Configure(EntityTypeBuilder<ActiveSession> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.DeviceId).HasColumnName("device_id").IsRequired();
        builder.Property(x => x.DeviceLabel).HasColumnName("device_label").IsRequired();
        builder.Property(x => x.DeviceType).HasColumnName("device_type").IsRequired();
        builder.Property(x => x.FirstSeenAt).HasColumnName("first_seen_at")
            .HasDefaultValueSql("now()");
        builder.Property(x => x.LastActivityAt).HasColumnName("last_activity_at")
            .HasDefaultValueSql("now()");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.RefreshTokenId).HasColumnName("refresh_token_id").IsRequired();

        builder.ToTable("active_session", t =>
            t.HasCheckConstraint("CK_active_session_device_type",
                "device_type IN ('phone','tablet','desktop','web')"));

        // Partial index: active sessions per user
        builder.HasIndex(x => new { x.UserId, x.RevokedAt })
            .HasFilter("revoked_at IS NULL");
    }
}
