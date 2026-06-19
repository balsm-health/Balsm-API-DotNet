using Balsm.Deletion.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Deletion.Infrastructure.Configuration;

public sealed class DeletionLogConfiguration : IEntityTypeConfiguration<DeletionLog>
{
    public void Configure(EntityTypeBuilder<DeletionLog> builder)
    {
        builder.ToTable("deletion_log", t =>
        {
            t.HasCheckConstraint("CK_deletion_log_reason_code",
                "reason_code IS NULL OR reason_code IN ('user_request','cancelled','support_request')");
            t.HasCheckConstraint("CK_deletion_log_apple_revoke_status",
                "apple_revoke_status IS NULL OR apple_revoke_status IN ('not_applicable','succeeded','failed_final','failed_retrying')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserIdHash).HasColumnName("user_id_hash").IsRequired();
        builder.Property(x => x.CountryCodeAtDeletion).HasColumnName("country_code_at_deletion")
            .HasMaxLength(2).IsRequired();
        builder.Property(x => x.ReasonCode).HasColumnName("reason_code");
        builder.Property(x => x.AppleRevokeStatus).HasColumnName("apple_revoke_status");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at")
            .HasDefaultValueSql("now()");
        builder.Property(x => x.PurgeAt).HasColumnName("purge_at").IsRequired();

    }
}
