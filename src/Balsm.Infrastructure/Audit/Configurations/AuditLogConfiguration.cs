using Balsm.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Infrastructure.Audit.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Sequence)
            .ValueGeneratedOnAdd();
        builder.Property(e => e.Module).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Action).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Actor).HasMaxLength(256);
        builder.Property(e => e.SourceIp).HasMaxLength(45);
        builder.Property(e => e.TargetType).HasMaxLength(100);
        builder.Property(e => e.TargetId).HasMaxLength(36);
        builder.Property(e => e.CorrelationId).HasMaxLength(36);

        builder.HasIndex(e => new { e.OccurredAt, e.Sequence })
            .IsDescending()
            .HasDatabaseName("IX_AuditLog_OccurredAt_Sequence_Desc");

        builder.HasIndex(e => new { e.Module, e.Action })
            .HasDatabaseName("IX_AuditLog_Module_Action");

        // Override global soft-delete filter — audit logs are never soft-filtered
        builder.HasQueryFilter(_ => true);
    }
}
