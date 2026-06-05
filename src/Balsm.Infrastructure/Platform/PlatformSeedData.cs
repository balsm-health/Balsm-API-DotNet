using Microsoft.EntityFrameworkCore;

namespace Balsm.Infrastructure.Platform;

public static class PlatformSeedData
{
    public static void SeedDefaultConfig(ModelBuilder modelBuilder)
    {
        var defaults = new[]
        {
            ("mode",                      "Standalone"),
            ("bind_http_port",            "5050"),
            ("bind_https_port",           "5051"),
            ("backup_cron",               "0 2 * * *"),
            ("backup_retention",          "30"),
            ("backup_directory",          "backups"),
            ("audit_retention_years",     "2"),
            ("audit_retention_cron",      "0 3 * * *"),
            ("session_idle_minutes",      "30"),
            ("lockout_threshold_count",   "5"),
            ("lockout_duration_minutes",  "15"),
            ("locale_default",            "en"),
            ("tunnel_provider",           ""),
        };

        var seedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var entries = defaults.Select((kv, i) => new ServerConfigEntry(new Guid($"00000000-0000-0000-0000-{(i + 1):D12}"))
        {
            Key = kv.Item1,
            Value = kv.Item2,
            CreatedAt = seedTime,
        }).ToArray();

        modelBuilder.Entity<ServerConfigEntry>().HasData(entries);
    }
}
