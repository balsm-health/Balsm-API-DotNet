using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// Boots the real host against throwaway SQLite files — no container, no Docker.
///
/// For endpoints that touch no database. Module health probes are exactly that
/// by design: liveness only, no DbContext, with readiness gated separately by
/// ReadinessGate/MigrationRunner. Requiring a Postgres container to assert that
/// a DB-free probe answers 200 makes the test un-runnable wherever Docker or
/// Docker Hub is unavailable, which is how mandated coverage quietly stops
/// being run.
///
/// Use <see cref="TestWebAppFactory"/> instead for anything that reads or writes
/// real data — Postgres semantics differ from SQLite and a test that cares about
/// storage should exercise the real provider.
/// </summary>
public sealed class LiteWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _localDb = Path.Combine(Path.GetTempPath(), $"balsm-lite-{Guid.NewGuid():N}.db");
    private readonly string _cloudDb = Path.Combine(Path.GetTempPath(), $"balsm-lite-cloud-{Guid.NewGuid():N}.db");

    public Task InitializeAsync() => Task.CompletedTask;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting, not ConfigureAppConfiguration: with top-level Program the
        // app reads builder.Configuration while the WebApplicationBuilder is
        // being built, which is BEFORE ConfigureAppConfiguration callbacks are
        // applied. Configured that way, Jwt:Secret arrives empty and the bearer
        // handler throws IDX10703 on the first request — including anonymous
        // ones, because the middleware initialises regardless.
        foreach (var (key, value) in new Dictionary<string, string>
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:ConnectionString"] = $"Data Source={_localDb}",
            ["CloudDatabase:Provider"] = "Sqlite",
            ["CloudDatabase:ConnectionString"] = $"Data Source={_cloudDb}",
            ["Jwt:Secret"] = "test-secret-at-least-32-bytes-long!!",
            ["Jwt:Issuer"] = "balsm-test",
            ["Jwt:Audience"] = "balsm-app",
            ["Otp:HmacSecret"] = "test-otp-hmac-secret",
            ["DobEncryption:Key"] = Convert.ToBase64String(new byte[32]),
            ["CareTeamEncryption:Key"] = Convert.ToBase64String(new byte[32]),
            ["Recovery:Secret"] = "test-recovery-secret",
            ["Resend:ApiKey"] = "",
            ["Sentry:Dsn"] = "",
            // ~19k rows the health probes do not read.
            ["CareDirectory:ImportOnStartup"] = "false",
        })
        {
            builder.UseSetting(key, value);
        }

        builder.UseEnvironment("Testing");
    }

    public new Task DisposeAsync()
    {
        foreach (var path in new[] { _localDb, _cloudDb })
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
                // A still-open connection holding the file is not a test failure.
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>Shares one container-free host across the DB-free endpoint tests.</summary>
[CollectionDefinition(nameof(LiteWebAppCollection))]
public sealed class LiteWebAppCollection : ICollectionFixture<LiteWebAppFactory>;
