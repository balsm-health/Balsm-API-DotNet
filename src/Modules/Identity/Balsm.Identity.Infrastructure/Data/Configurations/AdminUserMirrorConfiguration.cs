using Balsm.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Identity.Infrastructure.Data.Configurations;

internal sealed class AdminUserMirrorConfiguration : IEntityTypeConfiguration<AdminUserMirror>
{
    public void Configure(EntityTypeBuilder<AdminUserMirror> builder)
    {
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Role).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Locale).HasMaxLength(10).IsRequired();

        // Enforce a single non-deleted admin user via filtered unique index
        builder.HasIndex(x => x.Id)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = 0")
            .HasDatabaseName("IX_AdminUserMirror_Active_Single");
    }
}
