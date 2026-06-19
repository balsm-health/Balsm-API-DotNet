using Balsm.Account.Domain.Entities;
using Balsm.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Account.Infrastructure.Data;

public sealed class AccountDbContext(
    DbContextOptions<AccountDbContext> options,
    IDomainEventDispatcher domainEventDispatcher) : BaseDbContext(options, domainEventDispatcher)
{
    public DbSet<UserAccount> UserAccounts { get; set; } = null!;
    public DbSet<UsernameReservation> UsernameReservations { get; set; } = null!;
    public DbSet<ReservedHandleBlocklist> ReservedHandleBlocklist { get; set; } = null!;
    public DbSet<UserAccountAuditLog> UserAccountAuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountDbContext).Assembly);
    }
}
