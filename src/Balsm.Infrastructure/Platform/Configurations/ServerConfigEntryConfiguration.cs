using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Infrastructure.Platform.Configurations;

internal sealed class ServerConfigEntryConfiguration : IEntityTypeConfiguration<ServerConfigEntry>
{
    public void Configure(EntityTypeBuilder<ServerConfigEntry> builder)
    {
        builder.ToTable("ServerConfigs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Key).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Value).IsRequired().HasMaxLength(2000);
        builder.HasIndex(e => e.Key).IsUnique().HasDatabaseName("UX_ServerConfig_Key");
    }
}
