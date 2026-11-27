using Balsm.Auth.Infrastructure.Data;
using Balsm.Geofence.Domain;
using Balsm.SharedKernel.Contracts;
using Balsm.SharedKernel.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Balsm.Auth.Tests;

/// <summary>
/// A SQLite in-memory Auth database for handler tests. The connection stays
/// open for the fixture's lifetime (a :memory: database dies with its last
/// connection). <see cref="Db"/> is the context handed to the handler;
/// <see cref="Fresh"/> opens a second context on the same database so
/// assertions read what was saved, not what the first context still tracks.
/// </summary>
internal sealed class AuthTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly List<AuthDbContext> _contexts = [];

    public AuthTestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        Db = Fresh();
        Db.Database.EnsureCreated();
    }

    public AuthDbContext Db { get; }

    public AuthDbContext Fresh()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>().UseSqlite(_connection).Options;
        var context = new AuthDbContext(options, new NullEventDispatcher());
        _contexts.Add(context);
        return context;
    }

    public void Dispose()
    {
        foreach (var context in _contexts) context.Dispose();
        _connection.Dispose();
    }

    private sealed class NullEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken ct) => Task.CompletedTask;
    }
}

/// Settings shared by the handler tests. Secrets are test-only literals.
internal static class TestSettings
{
    public const string JwtSecret = "test-secret-at-least-32-bytes-long!!";
    public const string RecoverySecret = "test-recovery-secret";
    public const string AppleClientId = "test.balsm.app";
    public const string GoogleClientId = "test-client.apps.example.test";

    public static IConfiguration Build(string? recoverySecret = RecoverySecret) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = JwtSecret,
            ["Otp:HmacSecret"] = "test-otp-hmac-secret",
            ["Recovery:Secret"] = recoverySecret,
            ["Apple:ClientId"] = AppleClientId,
            ["Google:ClientId"] = GoogleClientId,
        }).Build();
}

/// Returns a fixed account id and records what it was asked to create.
internal sealed class FakeAccountProvisioner(Guid accountId) : IUserAccountProvisioner
{
    public List<(string CountryCode, string PreferredLanguage)> Calls { get; } = [];

    public Task<Guid> ProvisionAsync(string countryCode, string preferredLanguage, CancellationToken ct = default)
    {
        Calls.Add((countryCode, preferredLanguage));
        return Task.FromResult(accountId);
    }

    public Task<Guid> ProvisionWithPreferredIdAsync(
        string countryCode, string preferredLanguage, Guid preferredId, CancellationToken ct = default)
    {
        Calls.Add((countryCode, preferredLanguage));
        return Task.FromResult(accountId);
    }
}

/// Denies exactly the country codes it is given.
internal sealed class FakeGeofenceService(params string[] deniedCountries) : IGeofenceService
{
    public Task<bool> IsDeniedAsync(string countryCode, CancellationToken ct = default) =>
        Task.FromResult(deniedCountries.Contains(countryCode));
}
