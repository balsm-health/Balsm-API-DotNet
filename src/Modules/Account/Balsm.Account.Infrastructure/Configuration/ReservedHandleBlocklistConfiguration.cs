using Balsm.Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Account.Infrastructure.Configuration;

public sealed class ReservedHandleBlocklistConfiguration : IEntityTypeConfiguration<ReservedHandleBlocklist>
{
    private static readonly (string Handle, DateTime AddedAt)[] SeedHandles =
    [
        ("admin", new DateTime(2026, 7, 5, 22, 28, 28, 9, DateTimeKind.Utc).AddTicks(7629)),
        ("balsm", new DateTime(2026, 7, 5, 22, 28, 28, 9, DateTimeKind.Utc).AddTicks(8257)),
        ("support", new DateTime(2026, 7, 5, 22, 28, 28, 9, DateTimeKind.Utc).AddTicks(8261)),
        ("api", new DateTime(2026, 7, 5, 22, 28, 28, 9, DateTimeKind.Utc).AddTicks(8262)),
        ("help", new DateTime(2026, 7, 5, 22, 28, 28, 9, DateTimeKind.Utc).AddTicks(8263)),
        ("null", new DateTime(2026, 7, 5, 22, 28, 28, 9, DateTimeKind.Utc).AddTicks(8264)),
        ("health", new DateTime(2026, 7, 5, 22, 28, 28, 9, DateTimeKind.Utc).AddTicks(8268))
    ];

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
            SeedHandles.Select(seed => new
            {
                HandleNormalized = seed.Handle,
                AddedBy = "system",
                seed.AddedAt
            }));
    }
}
