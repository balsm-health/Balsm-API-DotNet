using System.Reflection;
using Balsm.API.Tests.Diagnostics;
using Balsm.API.Tests.Platform;
using Balsm.Infrastructure.Audit;
using Balsm.Infrastructure.Platform;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.API.Tests.Audit;

public sealed class AuditRetentionJobTests : IDisposable
{
    private readonly PlatformDbHarness _harness = new();
    private readonly ListLogger<AuditRetentionJob> _logger = new();
    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task ExecuteAsync_SeededSchedule_WaitsForTheNextOccurrence()
    {
        using var services = Services(withDb: true);

        await BackgroundServiceRunner.RunUntilAsync(Job(services), () => Logged("Next audit retention run"));

        Logged("Next audit retention run").Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_InvalidCron_FallsBackToTheDefaultSchedule()
    {
        var entry = await _harness.Db.ServerConfigs.SingleAsync(c => c.Key == "audit_retention_cron");
        entry.Value = "whenever";
        await _harness.Db.SaveChangesAsync();
        using var services = Services(withDb: true);

        await BackgroundServiceRunner.RunUntilAsync(Job(services), () => Logged("Next audit retention run"));

        Logged("Invalid audit_retention_cron").Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_NoConfigRows_UsesTheDefaults()
    {
        _harness.Db.ServerConfigs.RemoveRange(_harness.Db.ServerConfigs);
        await _harness.Db.SaveChangesAsync();
        using var services = Services(withDb: true);

        await BackgroundServiceRunner.RunUntilAsync(Job(services), () => Logged("Next audit retention run"));

        Logged("Invalid audit_retention_cron").Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ConfigUnreadable_LogsAndRetries()
    {
        using var services = Services(withDb: false);

        await BackgroundServiceRunner.RunUntilAsync(Job(services), () => Logged("Failed to read audit retention config"));

        Logged("Failed to read audit retention config").Should().BeTrue();
    }

    [Fact]
    public async Task RunRetention_NothingOldEnough_ArchivesNothing()
    {
        await SeedLogAsync(1, DateTime.UtcNow.AddDays(-1));
        using var services = Services(withDb: true);

        await RunRetentionAsync(services, retentionYears: 2);

        (await _harness.NewContext().AuditArchives.CountAsync()).Should().Be(0);
    }

    private bool Logged(string fragment) => _logger.Messages.Any(m => m.Contains(fragment));

    private AuditRetentionJob Job(ServiceProvider services) =>
        new(services.GetRequiredService<IServiceScopeFactory>(), _logger);

    private ServiceProvider Services(bool withDb)
    {
        var services = new ServiceCollection();
        if (withDb)
        {
            services.AddScoped(_ => _harness.NewContext());
            services.AddScoped<AuditExportSink>();
        }
        return services.BuildServiceProvider();
    }

    private async Task SeedLogAsync(long sequence, DateTime occurredAt)
    {
        // Sequence is set explicitly: SQLite has no generator for it.
        _harness.Db.AuditLogs.Add(new AuditLog
        {
            Sequence = sequence,
            Module = "Account",
            Action = "Updated",
            OccurredAt = occurredAt,
        });
        await _harness.Db.SaveChangesAsync();
    }

    // The retention pass only runs at the next cron occurrence, at least a
    // minute away; the pass itself is exercised directly instead.
    private Task RunRetentionAsync(ServiceProvider services, int retentionYears) =>
        (Task)typeof(AuditRetentionJob)
            .GetMethod("RunRetentionAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(Job(services), [retentionYears, CancellationToken.None])!;
}
