using Balsm.Identity.Infrastructure;
using Balsm.Identity.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Identity.Tests;

/// <summary>
/// Wires the Identity module through its own <c>AddIdentityInfrastructure</c> (the repository
/// and unit of work are internal, so they are reached through their public interfaces) against
/// a named SQLite in-memory database. Each scope's DbContext opens its own connection, so a
/// keeper connection holds the shared-cache database open for the lifetime of the host.
/// </summary>
internal sealed class IdentityTestHost : IDisposable
{
    private static int _instanceCounter;

    private readonly SqliteConnection _keeper;

    public IdentityTestHost()
    {
        var connectionString =
            $"Data Source=identity-tests-{Interlocked.Increment(ref _instanceCounter)};Mode=Memory;Cache=Shared";
        _keeper = new SqliteConnection(connectionString);
        _keeper.Open();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:ConnectionString"] = connectionString,
        }).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventDispatcher, NullDomainEventDispatcher>();
        services.AddIdentityInfrastructure(configuration);
        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.EnsureCreated();
    }

    public ServiceProvider Services { get; }

    public void Dispose()
    {
        Services.Dispose();
        _keeper.Dispose();
    }
}

internal sealed class NullDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
