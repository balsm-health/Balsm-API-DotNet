using Balsm.Identity.Domain.FamilyLinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Identity.Infrastructure.Data.Configurations;

internal sealed class FamilyLinkAuditEntryConfiguration : IEntityTypeConfiguration<FamilyLinkAuditEntry>
{
    public void Configure(EntityTypeBuilder<FamilyLinkAuditEntry> builder)
    {
        builder.ToTable("family_link_audit");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.FamilyLinkId).HasColumnName("family_link_id").IsRequired();
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id").IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(32).IsRequired()
            .HasConversion<string>();
        builder.Property(x => x.FromStatus).HasColumnName("from_status").HasMaxLength(16)
            .HasConversion<string?>();
        builder.Property(x => x.ToStatus).HasColumnName("to_status").HasMaxLength(16).IsRequired()
            .HasConversion<string>();
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128);
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();

        // No foreign key: an audit row must outlive anything it describes.
        builder.HasIndex(x => new { x.FamilyLinkId, x.OccurredAt });
    }
}
