using Balsm.Auth.Domain.Entities;
using Balsm.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Auth.Infrastructure.Data;

public sealed class AuthDbContext(
    DbContextOptions<AuthDbContext> options,
    IDomainEventDispatcher domainEventDispatcher) : BaseDbContext(options, domainEventDispatcher)
{
    public DbSet<UserIdentity> UserIdentities { get; set; } = null!;
    public DbSet<UserRefreshToken> UserRefreshTokens { get; set; } = null!;
    public DbSet<AccountLockout> AccountLockouts { get; set; } = null!;
    public DbSet<OtpChallenge> OtpChallenges { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
