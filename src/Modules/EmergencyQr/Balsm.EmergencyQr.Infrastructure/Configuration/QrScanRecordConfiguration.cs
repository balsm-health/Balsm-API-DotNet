using Balsm.EmergencyQr.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.EmergencyQr.Infrastructure.Configuration;

public sealed class QrScanRecordConfiguration : IEntityTypeConfiguration<QrScanRecord>
{
    public void Configure(EntityTypeBuilder<QrScanRecord> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TokenId).HasColumnName("token_id").IsRequired();
        builder.Property(x => x.OwnerUserId).HasColumnName("owner_user_id").IsRequired();
        builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at").IsRequired();
        builder.Property(x => x.ClientClass).HasColumnName("client_class")
            .HasMaxLength(16).IsRequired();
        builder.Property(x => x.Country).HasColumnName("country").HasMaxLength(2);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.Ignore(x => x.IsDeleted);
        builder.Ignore(x => x.DeletedAt);
        builder.Ignore(x => x.DeletedBy);
        builder.Ignore(x => x.UpdatedAt);
        builder.Ignore(x => x.UpdatedBy);
        builder.Ignore(x => x.CreatedBy);

        builder.ToTable("qr_scan_record");

        // Owner's history reads newest-first.
        builder.HasIndex(x => new { x.OwnerUserId, x.ResolvedAt });
    }
}
