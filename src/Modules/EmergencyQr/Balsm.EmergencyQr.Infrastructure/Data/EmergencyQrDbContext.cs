using Balsm.EmergencyQr.Domain.Entities;
using Balsm.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Balsm.EmergencyQr.Infrastructure.Data;

public sealed class EmergencyQrDbContext(
    DbContextOptions<EmergencyQrDbContext> options,
    IDomainEventDispatcher domainEventDispatcher) : BaseDbContext(options, domainEventDispatcher)
{
    public DbSet<EmergencyQrToken> EmergencyQrTokens { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EmergencyQrDbContext).Assembly);
    }
}
