using Balsm.Identity.Domain.FamilyLinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Identity.Infrastructure.Data.Configurations;

internal sealed class FamilyLinkConfiguration : IEntityTypeConfiguration<FamilyLink>
{
    public void Configure(EntityTypeBuilder<FamilyLink> builder)
    {
        builder.ToTable("family_link");
        builder.HasKey(x => x.Id);
        // Client-minted (the create is idempotent on it) — never server-assigned.
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.RequesterUserId).HasColumnName("requester_user_id").IsRequired();
        builder.Property(x => x.RecipientUserId).HasColumnName("recipient_user_id").IsRequired();
        builder.Property(x => x.PairLowUserId).HasColumnName("pair_low_user_id").IsRequired();
        builder.Property(x => x.PairHighUserId).HasColumnName("pair_high_user_id").IsRequired();
        builder.Property(x => x.Relation).HasColumnName("relation").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired()
            .HasConversion(FamilyLinkStatusConverter.Instance);
        builder.Property(x => x.RequesterPublicKey).HasColumnName("requester_public_key");
        builder.Property(x => x.RecipientPublicKey).HasColumnName("recipient_public_key");
        builder.Property(x => x.RespondedAt).HasColumnName("responded_at");
        builder.Property(x => x.LinkedAt).HasColumnName("linked_at");
        builder.Property(x => x.EndedAt).HasColumnName("ended_at");
        builder.Property(x => x.EndedByUserId).HasColumnName("ended_by_user_id");
        builder.Property(x => x.ConcurrencyStamp).HasColumnName("concurrency_stamp").IsConcurrencyToken();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        // Never deleted: status carries the lifecycle, so the soft-delete columns
        // (and the global IsDeleted filter they would switch on) do not apply.
        builder.Ignore(x => x.IsDeleted);
        builder.Ignore(x => x.DeletedAt);
        builder.Ignore(x => x.DeletedBy);
        builder.Ignore(x => x.CreatedBy);
        builder.Ignore(x => x.UpdatedBy);

        builder.HasMany(x => x.Consents).WithOne().HasForeignKey(c => c.FamilyLinkId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Consents).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(x => x.CardShares).WithOne().HasForeignKey(c => c.FamilyLinkId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.CardShares).UsePropertyAccessMode(PropertyAccessMode.Field);

        // One open request or link per pair, in either direction — the race-safe
        // backstop behind the handler's check. The filter is provider-neutral:
        // a quoted identifier and string literals parse the same on SQLite and Postgres.
        builder.HasIndex(x => new { x.PairLowUserId, x.PairHighUserId })
            .IsUnique()
            .HasFilter("\"status\" IN ('pending', 'linked')")
            .HasDatabaseName("ux_family_link_open_pair");
        builder.HasIndex(x => new { x.RecipientUserId, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.RequesterUserId, x.Status, x.CreatedAt });
    }
}
