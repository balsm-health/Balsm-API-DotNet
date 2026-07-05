using Balsm.Geofence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Geofence.Infrastructure.Configuration;

public sealed class DeniedCountryBlocklistConfiguration : IEntityTypeConfiguration<DeniedCountryBlocklist>
{
    private static readonly (string CountryCode, DateTime AddedAt)[] SeedCountries =
    [
        ("CU", new DateTime(2026, 7, 5, 22, 28, 39, 875, DateTimeKind.Utc).AddTicks(4706)),
        ("IR", new DateTime(2026, 7, 5, 22, 28, 39, 875, DateTimeKind.Utc).AddTicks(5249)),
        ("KP", new DateTime(2026, 7, 5, 22, 28, 39, 875, DateTimeKind.Utc).AddTicks(5253)),
        ("SY", new DateTime(2026, 7, 5, 22, 28, 39, 875, DateTimeKind.Utc).AddTicks(5254))
    ];

    public void Configure(EntityTypeBuilder<DeniedCountryBlocklist> builder)
    {
        builder.ToTable("denied_country_blocklist", t =>
            t.HasCheckConstraint("CK_denied_country_blocklist_source",
                "source IN ('ofac','apple_denied','google_denied','manual')"));
        builder.HasKey(x => x.CountryCode);
        builder.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(2);
        builder.Property(x => x.Source).HasColumnName("source").IsRequired();
        builder.Property(x => x.AddedAt).HasColumnName("added_at").HasDefaultValueSql("now()");

        builder.HasData(
            SeedCountries.Select(seed => new
            {
                seed.CountryCode,
                Source = "ofac",
                seed.AddedAt
            }));
    }
}
