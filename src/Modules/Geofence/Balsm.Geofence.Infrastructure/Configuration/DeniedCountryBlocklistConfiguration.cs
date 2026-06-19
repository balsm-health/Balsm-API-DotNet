using Balsm.Geofence.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Geofence.Infrastructure.Configuration;

public sealed class DeniedCountryBlocklistConfiguration : IEntityTypeConfiguration<DeniedCountryBlocklist>
{
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
            DeniedCountryBlocklist.Create("CU", "ofac"),
            DeniedCountryBlocklist.Create("IR", "ofac"),
            DeniedCountryBlocklist.Create("KP", "ofac"),
            DeniedCountryBlocklist.Create("SY", "ofac")
        );
    }
}
