using Balsm.Infrastructure.Lifecycle;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Infrastructure.Lifecycle.Configurations;

internal sealed class MigrationStateRecordConfiguration
    : IEntityTypeConfiguration<MigrationStateRecord>
{
    public void Configure(EntityTypeBuilder<MigrationStateRecord> builder)
    {
        builder.ToTable("MigrationStateRecords");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.DbContextName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.MigrationId).IsRequired().HasMaxLength(200);
        builder.Property(e => e.ErrorMessage).HasMaxLength(4000);
    }
}
