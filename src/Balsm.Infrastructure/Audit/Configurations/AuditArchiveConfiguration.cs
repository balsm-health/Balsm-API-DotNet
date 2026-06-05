using Balsm.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Infrastructure.Audit.Configurations;

internal sealed class AuditArchiveConfiguration : IEntityTypeConfiguration<AuditArchive>
{
    public void Configure(EntityTypeBuilder<AuditArchive> builder)
    {
        builder.ToTable("AuditArchives");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Filename).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Path).IsRequired().HasMaxLength(1024);
        builder.Property(e => e.Sha256).IsRequired().HasMaxLength(64);
        builder.HasIndex(e => e.Filename)
            .IsUnique()
            .HasDatabaseName("UX_AuditArchive_Filename");
    }
}
