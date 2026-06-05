using Balsm.Infrastructure.Audit;
using Balsm.Infrastructure.Backup;
using Balsm.Infrastructure.Data;
using Balsm.Infrastructure.Lifecycle;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Infrastructure.Platform;

public sealed class PlatformDbContext(
    DbContextOptions<PlatformDbContext> options,
    IDomainEventDispatcher domainEventDispatcher)
    : BaseDbContext(options, domainEventDispatcher)
{
    public DbSet<ServerConfigEntry> ServerConfigs { get; set; } = null!;
    public DbSet<BackupFile> BackupFiles { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<AuditArchive> AuditArchives { get; set; } = null!;
    public DbSet<MigrationStateRecord> MigrationStateRecords { get; set; } = null!;
    public DbSet<LockoutRecord> LockoutRecords { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("platform");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly,
            t => t.Namespace?.StartsWith("Balsm.Infrastructure.Platform.Configurations") == true
              || t.Namespace?.StartsWith("Balsm.Infrastructure.Audit.Configurations") == true
              || t.Namespace?.StartsWith("Balsm.Infrastructure.Backup.Configurations") == true
              || t.Namespace?.StartsWith("Balsm.Infrastructure.Lifecycle.Configurations") == true);
        PlatformSeedData.SeedDefaultConfig(modelBuilder);
    }
}
