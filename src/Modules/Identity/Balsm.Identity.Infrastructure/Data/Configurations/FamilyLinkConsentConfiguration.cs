using Balsm.Identity.Domain.FamilyLinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Identity.Infrastructure.Data.Configurations;

internal sealed class FamilyLinkConsentConfiguration : IEntityTypeConfiguration<FamilyLinkConsent>
{
    public void Configure(EntityTypeBuilder<FamilyLinkConsent> builder)
    {
        builder.ToTable("family_link_consent");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.FamilyLinkId).HasColumnName("family_link_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(32).IsRequired()
            .HasConversion<string>();
        builder.Property(x => x.GrantedAt).HasColumnName("granted_at").IsRequired();
        builder.Property(x => x.WithdrawnAt).HasColumnName("withdrawn_at");
        builder.Ignore(x => x.IsActive);

        builder.HasIndex(x => new { x.FamilyLinkId, x.UserId });
    }
}
