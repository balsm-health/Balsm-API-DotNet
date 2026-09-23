using Balsm.CareTeam.Domain.Entities;
using Balsm.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Balsm.CareTeam.Infrastructure.Data;

public sealed class CareTeamDbContext(
    DbContextOptions<CareTeamDbContext> options,
    IDomainEventDispatcher domainEventDispatcher) : BaseDbContext(options, domainEventDispatcher)
{
    public DbSet<CareProvider> CareProviders { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CareTeamDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
