using System.Reflection;
using Balsm.CareDirectory.Infrastructure.Data;
using Balsm.CareDirectory.Infrastructure.MapPacks;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Balsm.CareDirectory.Tests;

/// <summary>
/// The nightly cron wrapper around <see cref="MapPackExportRunner"/>. One run
/// is a private step behind a cron wait, so it is invoked directly here; the
/// registry it loads is the real <c>data/map-packs/governorates.json</c>
/// copied to the test output. The bucket is a substitute — no network.
/// </summary>
public sealed class MapPackExportJobTests : IDisposable
{
    private static readonly MapPackR2Options Configured = new()
    {
        AccountId = "account-test",
        AccessKeyId = "key-test",
        SecretAccessKey = "secret-test",
        Bucket = "bucket-test",
        CdnBaseUrl = "https://cdn.example.test",
    };

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;
    private readonly IMapPackObjectStore _store = Substitute.For<IMapPackObjectStore>();
    private readonly IHostEnvironment _environment = Substitute.For<IHostEnvironment>();

    public MapPackExportJobTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IDomainEventDispatcher>());
        services.AddDbContext<CareDirectoryDbContext>(o => o.UseSqlite(_connection));
        services.AddSingleton(_store);
        _services = services.BuildServiceProvider();
        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CareDirectoryDbContext>().Database.EnsureCreated();

        _store.ListAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<R2Object>());
        _environment.ContentRootPath.Returns(AppContext.BaseDirectory);
    }

    [Fact]
    public async Task RunOnce_R2NotConfigured_NeverTouchesTheBucket()
    {
        var job = Job(new MapPackR2Options(), Substitute.For<ILogger<MapPackExportJob>>());

        await RunOnceAsync(job);

        await _store.DidNotReceiveWithAnyArgs().ListAsync(default!, default);
    }

    [Fact]
    public async Task RunOnce_Configured_ReconcilesAgainstTheBucket()
    {
        var job = Job(Configured, Substitute.For<ILogger<MapPackExportJob>>());

        await RunOnceAsync(job);

        await _store.ReceivedWithAnyArgs().ListAsync(default!, default);
    }

    [Fact]
    public async Task StopAsync_WhileWaitingForTheNextRun_EndsTheLoop()
    {
        var logger = new FirstLogSignal();
        using var job = Job(Configured, logger);
        await job.StartAsync(CancellationToken.None);
        await logger.Logged.WaitAsync(TimeSpan.FromSeconds(10));

        await job.StopAsync(CancellationToken.None);

        job.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue();
        await _store.DidNotReceiveWithAnyArgs().ListAsync(default!, default);
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    private MapPackExportJob Job(MapPackR2Options options, ILogger<MapPackExportJob> logger) => new(
        _services.GetRequiredService<IServiceScopeFactory>(), Options.Create(options), _environment, logger);

    private static Task RunOnceAsync(MapPackExportJob job)
    {
        var runOnce = typeof(MapPackExportJob).GetMethod("RunOnceAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)runOnce.Invoke(job, [CancellationToken.None])!;
    }

    /// <summary>Completes once the job logs its first line — the "next run at" line it writes just before waiting.</summary>
    private sealed class FirstLogSignal : ILogger<MapPackExportJob>
    {
        private readonly TaskCompletionSource _logged = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Logged => _logged.Task;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _logged.TrySetResult();
    }
}
