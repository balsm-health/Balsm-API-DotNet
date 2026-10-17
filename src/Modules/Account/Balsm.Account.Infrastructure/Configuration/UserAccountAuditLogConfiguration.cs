using System.Net;
using Balsm.Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Account.Infrastructure.Configuration;

public sealed class UserAccountAuditLogConfiguration : IEntityTypeConfiguration<UserAccountAuditLog>
{
    public void Configure(EntityTypeBuilder<UserAccountAuditLog> builder)
    {
        builder.ToTable("user_account_audit_log");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.TargetUserId).HasColumnName("target_user_id").IsRequired();
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id").IsRequired();
        builder.Property(x => x.ReadAt).HasColumnName("read_at").HasDefaultValueSql("now()");
        builder.Property(x => x.SourceIp)
            .HasColumnName("source_ip")
            .HasColumnType("inet")
            .HasConversion(
                s => s == null ? null : IPAddress.Parse(s),
                ip => ip == null ? null : ip.ToString());
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").IsRequired();

        builder.HasIndex(x => new { x.TargetUserId, x.ReadAt });
    }
}
