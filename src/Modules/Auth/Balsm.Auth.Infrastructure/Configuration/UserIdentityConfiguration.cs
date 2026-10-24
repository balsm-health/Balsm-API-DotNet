using Balsm.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Auth.Infrastructure.Configuration;

public sealed class UserIdentityConfiguration : IEntityTypeConfiguration<UserIdentity>
{
    public void Configure(EntityTypeBuilder<UserIdentity> builder)
    {
        builder.ToTable("user_identities", t =>
            t.HasCheckConstraint("CK_user_identities_provider",
                "provider IN ('email','google','apple')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Provider).HasColumnName("provider").IsRequired()
            .HasMaxLength(20);
        builder.Property(x => x.ProviderSubject).HasColumnName("provider_subject").IsRequired();
        builder.Property(x => x.EmailNormalized).HasColumnName("email_normalized")
            .HasColumnType("citext");
        builder.Property(x => x.EmailConfirmedAt).HasColumnName("email_confirmed_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at")
            .HasDefaultValueSql("now()");
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash");
        builder.Property(x => x.PasswordSetAt).HasColumnName("password_set_at");

        builder.HasIndex(x => new { x.Provider, x.ProviderSubject }).IsUnique();
        // Partial unique on email_normalized for email provider enforced at DB level via migration
    }
}
