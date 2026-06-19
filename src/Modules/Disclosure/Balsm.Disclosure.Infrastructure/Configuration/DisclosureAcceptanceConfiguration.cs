using Balsm.Disclosure.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Disclosure.Infrastructure.Configuration;

public sealed class DisclosureAcceptanceConfiguration : IEntityTypeConfiguration<DisclosureAcceptance>
{
    public void Configure(EntityTypeBuilder<DisclosureAcceptance> builder)
    {
        builder.ToTable("disclosure_acceptance");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.DisclosureId).HasColumnName("disclosure_id").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
        builder.Property(x => x.CountryCodeAtAccept).HasColumnName("country_code_at_accept")
            .HasMaxLength(2).IsRequired();
        builder.Property(x => x.SupervisoryAuthorityNameAtAccept)
            .HasColumnName("supervisory_authority_name_at_accept").IsRequired();
        builder.Property(x => x.PreferredLanguageAtAccept)
            .HasColumnName("preferred_language_at_accept").IsRequired();
        builder.Property(x => x.AcceptedAt).HasColumnName("accepted_at")
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => new { x.UserId, x.DisclosureId, x.Version }).IsUnique();
    }
}
