using Balsm.Identity.Domain.FamilyLinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Identity.Infrastructure.Data.Configurations;

internal sealed class FamilyLinkCardShareConfiguration : IEntityTypeConfiguration<FamilyLinkCardShare>
{
    public void Configure(EntityTypeBuilder<FamilyLinkCardShare> builder)
    {
        builder.ToTable("family_link_card_share");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.FamilyLinkId).HasColumnName("family_link_id").IsRequired();
        builder.Property(x => x.OwnerUserId).HasColumnName("owner_user_id").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
        // Opaque sealed envelope — the server holds no key that opens it.
        builder.Property(x => x.EphemeralPublicKey).HasColumnName("ephemeral_public_key").IsRequired();
        builder.Property(x => x.Nonce).HasColumnName("nonce").IsRequired();
        builder.Property(x => x.Ciphertext).HasColumnName("ciphertext").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // One card per owner per link; each direction is independent.
        builder.HasIndex(x => new { x.FamilyLinkId, x.OwnerUserId }).IsUnique();
    }
}
