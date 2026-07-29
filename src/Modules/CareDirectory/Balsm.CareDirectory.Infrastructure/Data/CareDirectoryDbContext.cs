using Balsm.CareDirectory.Domain.Entities;
using Balsm.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Balsm.CareDirectory.Infrastructure.Data;

public sealed class CareDirectoryDbContext(
    DbContextOptions<CareDirectoryDbContext> options,
    IDomainEventDispatcher domainEventDispatcher) : BaseDbContext(options, domainEventDispatcher)
{
    public DbSet<CarePlace> CarePlaces { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CareDirectoryDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
