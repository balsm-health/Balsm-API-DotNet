using Balsm.Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Account.Infrastructure.Configuration;

public sealed class UsernameReservationConfiguration : IEntityTypeConfiguration<UsernameReservation>
{
    public void Configure(EntityTypeBuilder<UsernameReservation> builder)
    {
        builder.ToTable("username_reservation");
        builder.HasKey(x => x.HandleNormalized);
        builder.Property(x => x.HandleNormalized).HasColumnName("handle_normalized")
            .HasColumnType("citext");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.ClaimedAt).HasColumnName("claimed_at")
            .HasDefaultValueSql("now()");
        builder.Property(x => x.ReleasedAt).HasColumnName("released_at");
    }
}
