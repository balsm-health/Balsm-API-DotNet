using Balsm.Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Account.Infrastructure.Configuration;

public sealed class ReservedHandleBlocklistConfiguration : IEntityTypeConfiguration<ReservedHandleBlocklist>
{
    public void Configure(EntityTypeBuilder<ReservedHandleBlocklist> builder)
    {
        builder.ToTable("reserved_handle_blocklist");
        builder.HasKey(x => x.HandleNormalized);
        builder.Property(x => x.HandleNormalized).HasColumnName("handle_normalized")
            .HasColumnType("citext");
        builder.Property(x => x.AddedBy).HasColumnName("added_by")
            .HasDefaultValue("system").IsRequired();
        builder.Property(x => x.AddedAt).HasColumnName("added_at")
            .HasDefaultValueSql("now()");

        // T005 seed (FR-003)
        builder.HasData(
            ReservedHandleBlocklist.Create("admin"),
            ReservedHandleBlocklist.Create("balsm"),
            ReservedHandleBlocklist.Create("support"),
            ReservedHandleBlocklist.Create("api"),
            ReservedHandleBlocklist.Create("help"),
            ReservedHandleBlocklist.Create("null"),
            ReservedHandleBlocklist.Create("health")
        );
    }
}
