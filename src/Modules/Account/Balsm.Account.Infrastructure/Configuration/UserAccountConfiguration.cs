using Balsm.Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Account.Infrastructure.Configuration;

public sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("user_account", t =>
            t.HasCheckConstraint("CK_user_account_deletion_state",
                "deletion_state IN ('ACTIVE','DELETION_REQUESTED','DELETION_CANCELLED')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Handle).HasColumnName("handle").HasColumnType("citext");
        builder.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(64);
        builder.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(64);
        builder.Property(x => x.DisplayName).HasColumnName("display_name");
        builder.Property(x => x.Bio).HasColumnName("bio");
        builder.Property(x => x.Gender).HasColumnName("gender").HasMaxLength(16);
        builder.Property(x => x.Nationality).HasColumnName("nationality").HasMaxLength(64);
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(32);
        builder.Property(x => x.DateOfBirthCiphertext).HasColumnName("date_of_birth_ciphertext")
            .HasColumnType("bytea");
        builder.Property(x => x.NationalIdCiphertext).HasColumnName("national_id_ciphertext")
            .HasColumnType("bytea");
        builder.Property(x => x.CountryCode).HasColumnName("country_code").IsRequired()
            .HasMaxLength(2);
        builder.Property(x => x.PreferredLanguage).HasColumnName("preferred_language").IsRequired();
        builder.Property(x => x.DeletionState).HasColumnName("deletion_state")
            .HasConversion<string>().HasDefaultValue(DeletionState.ACTIVE).IsRequired();
        builder.Property(x => x.DeletionConfirmedAt).HasColumnName("deletion_confirmed_at");
        builder.Property(x => x.DeletionGraceUntil).HasColumnName("deletion_grace_until");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        builder.Ignore(x => x.IsDeleted);
        builder.Ignore(x => x.DeletedAt);
        builder.Ignore(x => x.DeletedBy);
        builder.Ignore(x => x.UpdatedBy);
        builder.Ignore(x => x.CreatedBy);

        builder.HasIndex(x => x.Handle).IsUnique().HasFilter("handle IS NOT NULL");
    }
}
