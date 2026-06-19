using Balsm.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Auth.Infrastructure.Configuration;

public sealed class AccountLockoutConfiguration : IEntityTypeConfiguration<AccountLockout>
{
    public void Configure(EntityTypeBuilder<AccountLockout> builder)
    {
        builder.ToTable("account_lockout", t =>
            t.HasCheckConstraint("CK_account_lockout_identifier_type",
                "identifier_type IN ('email','apple_sub','google_sub','handle')"));
        builder.HasKey(x => x.Identifier);
        builder.Property(x => x.Identifier).HasColumnName("identifier");
        builder.Property(x => x.IdentifierType).HasColumnName("identifier_type").IsRequired();
        builder.Property(x => x.FailedAttempts).HasColumnName("failed_attempts")
            .HasDefaultValue((short)0);
        builder.Property(x => x.RollingWindowStartedAt).HasColumnName("rolling_window_started_at")
            .HasDefaultValueSql("now()");
        builder.Property(x => x.LockedUntil).HasColumnName("locked_until");

    }
}
