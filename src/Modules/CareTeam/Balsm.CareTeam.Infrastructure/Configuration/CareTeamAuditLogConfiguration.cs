using Balsm.CareTeam.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.CareTeam.Infrastructure.Configuration;

public sealed class CareTeamAuditLogConfiguration : IEntityTypeConfiguration<CareTeamAuditLog>
{
    public void Configure(EntityTypeBuilder<CareTeamAuditLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.HealthProfileId).HasColumnName("health_profile_id").IsRequired();
        builder.Property(x => x.Actor).HasColumnName("actor").HasMaxLength(128);
        builder.Property(x => x.SourceIp).HasColumnName("source_ip").HasMaxLength(64);
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128);
        builder.Property(x => x.RowCount).HasColumnName("row_count").IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.Ignore(x => x.CreatedBy);
        builder.Ignore(x => x.UpdatedAt);
        builder.Ignore(x => x.UpdatedBy);
        builder.Ignore(x => x.IsDeleted);
        builder.Ignore(x => x.DeletedAt);
        builder.Ignore(x => x.DeletedBy);

        builder.ToTable("care_team_audit_log");
        builder.HasIndex(x => new { x.UserId, x.OccurredAt });
    }
}
