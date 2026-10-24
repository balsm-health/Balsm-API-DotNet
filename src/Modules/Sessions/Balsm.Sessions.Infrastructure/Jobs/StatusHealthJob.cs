using Balsm.Sessions.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Balsm.Sessions.Infrastructure.Jobs;

/// <summary>
/// Background job that probes DB connectivity every 30s and writes result to IMemoryCache.
/// StatusController reads from cache so /status never blocks on DB. FR-046b.
/// </summary>
public sealed class StatusHealthJob : BackgroundService
{
    public const string CacheKey = "status:health";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StatusHealthJob> _logger;

    public StatusHealthJob(
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        ILogger<StatusHealthJob> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProbeAsync(stoppingToken);

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProbeAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
            await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
            _cache.Set(CacheKey, new HealthStatus("operational", DateTime.UtcNow), TimeSpan.FromMinutes(2));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "StatusHealthJob DB probe failed");
            _cache.Set(CacheKey, new HealthStatus("degraded", DateTime.UtcNow), TimeSpan.FromMinutes(2));
        }
    }
}

public sealed record HealthStatus(string Status, DateTime CheckedAt);
