using Balsm.CareDirectory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.CareDirectory.Infrastructure.Configuration;

/// <summary>
/// EF mapping for the care directory.
///
/// There is deliberately NO HasData seed here. This class used to carry 12
/// hand-written rows that paired real Egyptian institutions with invented
/// contact details — a patient tapping "call" on a real hospital reached a
/// number nobody answers. Directory rows now come only from the import
/// pipeline, which records provenance for every one of them.
/// </summary>
public sealed class CarePlaceConfiguration : IEntityTypeConfiguration<CarePlace>
{
    public void Configure(EntityTypeBuilder<CarePlace> builder)
    {
        builder.ToTable("care_place");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Type).HasColumnName("type").IsRequired();

        // Nullable because no lawful source fills them for every row: Overture
        // carries one name per place, never a bilingual pair.
        builder.Property(x => x.NameEn).HasColumnName("name_en");
        builder.Property(x => x.NameAr).HasColumnName("name_ar");
        builder.Property(x => x.AddressEn).HasColumnName("address_en");
        builder.Property(x => x.AddressAr).HasColumnName("address_ar");

        builder.Property(x => x.Lat).HasColumnName("lat");
        builder.Property(x => x.Lng).HasColumnName("lng");

        // Overture has no opening-hours field and no ratings field at all.
        builder.Property(x => x.Hours).HasColumnName("hours");
        builder.Property(x => x.Phone).HasColumnName("phone");
        builder.Property(x => x.Rating).HasColumnName("rating");

        builder.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(2).IsRequired();

        builder.Property(x => x.ExternalId).HasColumnName("external_id").HasMaxLength(64);
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(16).IsRequired();
        builder.Property(x => x.Confidence).HasColumnName("confidence");

        builder.Property(x => x.NameArNorm).HasColumnName("name_ar_norm");
        builder.Property(x => x.AddressArNorm).HasColumnName("address_ar_norm");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => new { x.CountryCode, x.Type });

        // The upsert key. Unique so a re-import can never duplicate a place.
        // Curated-only rows carry a NULL external_id and do not collide with each
        // other: both Postgres and SQLite treat NULLs as distinct in a unique index.
        builder.HasIndex(x => x.ExternalId).IsUnique();

        // Search hits these, never the raw Arabic columns.
        builder.HasIndex(x => x.NameArNorm);
        builder.HasIndex(x => x.AddressArNorm);
    }
}
