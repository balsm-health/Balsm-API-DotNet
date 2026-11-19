using Balsm.Infrastructure.Audit;
using Balsm.Infrastructure.Maintenance;
using Balsm.Infrastructure.Platform;
using Balsm.SharedKernel.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Balsm.API.Tests.Maintenance;

/// <summary>A controllable clock so tests never read the real time.</summary>
internal sealed class ManualClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>
/// Real <see cref="MaintenanceModeService"/> over an in-memory SQLite
/// <see cref="PlatformDbContext"/> (the default provider), with a manual clock.
/// </summary>
internal sealed class MaintenanceHarness : IDisposable
{
    public static readonly DateTimeOffset Start = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public MaintenanceHarness(MaintenanceOptions? options = null)
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddScoped<IDomainEventDispatcher, NoopDispatcher>();
        services.AddDbContext<PlatformDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        _provider = services.BuildServiceProvider();

        using (var scope = _provider.CreateScope())
            scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.Migrate();

        Service = new MaintenanceModeService(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options ?? new MaintenanceOptions()),
            Clock,
            NullLogger<MaintenanceModeService>.Instance);
    }

    public ManualClock Clock { get; } = new(Start);

    public MaintenanceModeService Service { get; }

    public T Query<T>(Func<PlatformDbContext, T> read)
    {
        using var scope = _provider.CreateScope();
        return read(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    private sealed class NoopDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
