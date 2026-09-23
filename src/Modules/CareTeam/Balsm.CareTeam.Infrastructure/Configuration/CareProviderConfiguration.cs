using Balsm.CareTeam.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.CareTeam.Infrastructure.Configuration;

public sealed class CareProviderConfiguration : IEntityTypeConfiguration<CareProvider>
{
    public void Configure(EntityTypeBuilder<CareProvider> builder)
    {
        builder.HasKey(x => x.Id);
        // Device-minted UUIDv7 (FR-505) — never server-assigned, so no default.
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.HealthProfileId).HasColumnName("health_profile_id").IsRequired();

        // Plaintext: needed for filtering and the sync cursor, carries no free text (FR-503).
        builder.Property(x => x.Type).HasColumnName("type").HasMaxLength(16).IsRequired();

        // Encrypted free text (FR-502).
        builder.Property(x => x.Name).HasColumnName("name_ct").HasColumnType("bytea").IsRequired();
        builder.Property(x => x.Specialty).HasColumnName("specialty_ct").HasColumnType("bytea");
        builder.Property(x => x.Phone).HasColumnName("phone_ct").HasColumnType("bytea");
        builder.Property(x => x.Phone2).HasColumnName("phone2_ct").HasColumnType("bytea");
        builder.Property(x => x.Email).HasColumnName("email_ct").HasColumnType("bytea");
        builder.Property(x => x.Clinic).HasColumnName("clinic_ct").HasColumnType("bytea");
        builder.Property(x => x.Address).HasColumnName("address_ct").HasColumnType("bytea");
        builder.Property(x => x.MapUrl).HasColumnName("map_url_ct").HasColumnType("bytea");
        builder.Property(x => x.Notes).HasColumnName("notes_ct").HasColumnType("bytea");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        builder.Ignore(x => x.CreatedBy);
        builder.Ignore(x => x.UpdatedBy);
        builder.Ignore(x => x.DeletedBy);

        builder.ToTable("care_provider");

        // The incremental pull is (user_id, health_profile_id) filtered and
        // updated_at ordered — FR-508/FR-510.
        builder.HasIndex(x => new { x.UserId, x.HealthProfileId, x.UpdatedAt });
    }
}
