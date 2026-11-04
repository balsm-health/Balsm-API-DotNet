using Balsm.Identity.Domain;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Identity.Infrastructure.Data;

public sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options,
    IDomainEventDispatcher domainEventDispatcher) : BaseDbContext(options, domainEventDispatcher)
{
    public DbSet<AdminUserMirror> AdminUsers { get; set; } = null!;
    public DbSet<FamilyLink> FamilyLinks { get; set; } = null!;
    public DbSet<FamilyLinkCardShare> FamilyLinkCardShares { get; set; } = null!;
    public DbSet<FamilyLinkConsent> FamilyLinkConsents { get; set; } = null!;
    public DbSet<FamilyLinkAuditEntry> FamilyLinkAuditEntries { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
