using Balsm.Sessions.Infrastructure.Data;
using Balsm.Sessions.Infrastructure.Jobs;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.Sessions.Tests;

/// <summary>
/// Drives the job against a SQLite in-memory database (reachable) or a scope with
/// no SessionsDbContext registered (probe fails). The job's 30s interval is never
/// waited out: each test stops the job right after the first probe lands in cache.
/// </summary>
public sealed class StatusHealthJobTests : IDisposable
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public StatusHealthJobTests() => _connection.Open();

    public void Dispose()
    {
        _cache.Dispose();
        _connection.Dispose();
    }

    private ServiceProvider BuildProvider(bool databaseReachable)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventDispatcher, NullDomainEventDispatcher>();
        if (databaseReachable)
            services.AddDbContext<SessionsDbContext>(o => o.UseSqlite(_connection));
        return services.BuildServiceProvider();
    }

    private async Task<(HealthStatus Status, StatusHealthJob Job)> RunOneProbeAsync(
        bool databaseReachable, ILogger<StatusHealthJob>? logger = null)
    {
        await using var provider = BuildProvider(databaseReachable);
        var job = new StatusHealthJob(
            provider.GetRequiredService<IServiceScopeFactory>(),
            _cache,
            logger ?? NullLogger<StatusHealthJob>.Instance);

        await job.StartAsync(CancellationToken.None);
        var status = await WaitForStatusAsync();
        await job.StopAsync(CancellationToken.None);
        return (status, job);
    }

    private async Task<HealthStatus> WaitForStatusAsync()
    {
        using var timeout = new CancellationTokenSource(WaitLimit);
        HealthStatus? status;
        while (!_cache.TryGetValue(StatusHealthJob.CacheKey, out status) || status is null)
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        return status;
    }

    [Fact]
    public async Task ExecuteAsync_WhenDatabaseReachable_CachesOperationalStatus()
    {
        var (status, job) = await RunOneProbeAsync(databaseReachable: true);
        job.Dispose();

        status.Status.Should().Be("operational");
    }

    [Fact]
    public async Task ExecuteAsync_WhenProbeFails_CachesDegradedStatus()
    {
        var (status, job) = await RunOneProbeAsync(databaseReachable: false);
        job.Dispose();

        status.Status.Should().Be("degraded");
    }

    [Fact]
    public async Task ExecuteAsync_WhenProbeFails_LogsWarning()
    {
        var logger = new RecordingLogger();

        var (_, job) = await RunOneProbeAsync(databaseReachable: false, logger);
        job.Dispose();

        logger.Levels.Should().Contain(LogLevel.Warning);
    }

    [Fact]
    public async Task StopAsync_WhileWaitingForNextProbe_CompletesExecuteTask()
    {
        var (_, job) = await RunOneProbeAsync(databaseReachable: true);

        var executeTask = job.ExecuteTask;
        job.Dispose();

        executeTask!.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void CacheKey_Always_IsStatusHealth()
    {
        var key = StatusHealthJob.CacheKey;

        key.Should().Be("status:health");
    }

    private sealed class RecordingLogger : ILogger<StatusHealthJob>
    {
        private readonly List<LogLevel> _levels = [];

        public IReadOnlyList<LogLevel> Levels
        {
            get
            {
                lock (_levels)
                    return [.. _levels];
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_levels)
                _levels.Add(logLevel);
        }
    }
}
