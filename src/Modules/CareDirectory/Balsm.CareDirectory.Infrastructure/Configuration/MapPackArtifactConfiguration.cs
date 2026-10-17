using Balsm.CareDirectory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.CareDirectory.Infrastructure.Configuration;

/// <summary>
/// EF mapping for the offline map-pack manifest.
///
/// No HasData seed, for the same reason the directory has none: a row here
/// asserts that a file exists on the CDN, and a seed would assert it about
/// files nobody uploaded.
/// </summary>
public sealed class MapPackArtifactConfiguration : IEntityTypeConfiguration<MapPackArtifact>
{
    public void Configure(EntityTypeBuilder<MapPackArtifact> builder)
    {
        builder.ToTable("map_pack_artifact");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.GovernorateId).HasColumnName("governorate_id").IsRequired().HasMaxLength(64);
        builder.Property(x => x.NameEn).HasColumnName("name_en").IsRequired().HasMaxLength(128);
        builder.Property(x => x.NameAr).HasColumnName("name_ar").IsRequired().HasMaxLength(128);

        // Stored as the int it is: the set is closed and adding a kind is a
        // schema decision, not a string someone can typo into the table.
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<int>().IsRequired();

        builder.Property(x => x.Version).HasColumnName("version").IsRequired().HasMaxLength(16);
        builder.Property(x => x.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(x => x.Sha256).HasColumnName("sha256").IsRequired().HasMaxLength(64);
        builder.Property(x => x.Url).HasColumnName("url").IsRequired().HasMaxLength(512);
        builder.Property(x => x.PlaceCount).HasColumnName("place_count");

        builder.Property(x => x.West).HasColumnName("west").IsRequired();
        builder.Property(x => x.South).HasColumnName("south").IsRequired();
        builder.Property(x => x.East).HasColumnName("east").IsRequired();
        builder.Property(x => x.North).HasColumnName("north").IsRequired();

        builder.Property(x => x.PublishedAt).HasColumnName("published_at").IsRequired();

        // One row per governorate per kind. The nightly job upserts against
        // this: without it a failed-then-retried run would publish a second
        // Cairo places row and the endpoint would return the governorate twice.
        builder.HasIndex(x => new { x.GovernorateId, x.Kind }).IsUnique();
    }
}
