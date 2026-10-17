using Balsm.Infrastructure.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Infrastructure.Backup.Configurations;

internal sealed class BackupFileConfiguration : IEntityTypeConfiguration<BackupFile>
{
    public void Configure(EntityTypeBuilder<BackupFile> builder)
    {
        builder.ToTable("BackupFiles");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Filename).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Path).IsRequired().HasMaxLength(1024);
        builder.Property(e => e.Sha256).IsRequired().HasMaxLength(64);
        builder.HasIndex(e => e.CreatedAt)
            .IsDescending()
            .HasDatabaseName("IX_BackupFile_CreatedAt_Desc");
    }
}
