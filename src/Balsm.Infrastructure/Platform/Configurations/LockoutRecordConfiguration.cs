using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Infrastructure.Platform.Configurations;

internal sealed class LockoutRecordConfiguration : IEntityTypeConfiguration<LockoutRecord>
{
    public void Configure(EntityTypeBuilder<LockoutRecord> builder)
    {
        builder.ToTable("LockoutRecords");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.AdminEmail).IsRequired().HasMaxLength(256);
        builder.Property(e => e.SourceIp).IsRequired().HasMaxLength(45);
        builder.HasIndex(e => new { e.AdminEmail, e.SourceIp })
            .IsUnique()
            .HasDatabaseName("UX_Lockout_Email_Ip");
    }
}
