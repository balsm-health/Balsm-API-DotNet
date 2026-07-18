using Balsm.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.Auth.Infrastructure.Configuration;

public sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        builder.ToTable("otp_challenge");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.EmailNormalized).HasColumnName("email_normalized").IsRequired();
        builder.Property(x => x.CodeHash).HasColumnName("code_hash").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.ConsumedAt).HasColumnName("consumed_at");

        // Verify looks up the latest unconsumed challenge for an email.
        builder.HasIndex(x => new { x.EmailNormalized, x.ConsumedAt });
    }
}
