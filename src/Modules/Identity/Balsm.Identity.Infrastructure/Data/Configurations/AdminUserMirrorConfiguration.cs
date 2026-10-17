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

        // Enforce a single non-deleted admin user via filtered unique index.
        // Filter is provider-neutral: FALSE is a boolean literal on Npgsql and equals 0
        // on SQLite (>=3.23), so "IsDeleted" = 0 (SQLite) is avoided — that breaks on
        // Postgres where IsDeleted is a real boolean (42883: boolean = integer).
        builder.HasIndex(x => x.Id)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = FALSE")
            .HasDatabaseName("IX_AdminUserMirror_Active_Single");
    }
}
