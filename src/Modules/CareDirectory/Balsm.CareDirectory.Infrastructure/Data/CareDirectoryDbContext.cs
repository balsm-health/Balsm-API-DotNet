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

    /// <summary>Offline artifacts already uploaded to the CDN — the manifest
    /// behind GET /care/packs.</summary>
    public DbSet<MapPackArtifact> MapPackArtifacts { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CareDirectoryDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
