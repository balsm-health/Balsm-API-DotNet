using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// Spins up a Postgres container once per test collection. Starts the real ASP.NET host
/// with overridden CloudDatabase connection string.
/// </summary>
public sealed class TestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("balsm_test")
        .WithUsername("balsm")
        .WithPassword("balsm_test_pw")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CloudDatabase:Provider"] = "postgresql",
                ["CloudDatabase:ConnectionString"] = _postgres.GetConnectionString(),
                ["Jwt:Secret"] = "test-secret-at-least-32-bytes-long!!",
                ["Jwt:Issuer"] = "balsm-test",
                ["Jwt:Audience"] = "balsm-app",
                ["Otp:HmacSecret"] = "test-otp-hmac-secret",
                ["DobEncryption:Key"] = Convert.ToBase64String(new byte[32]),
                ["Recovery:Secret"] = "test-recovery-secret",
                // Disable Resend, reCAPTCHA, Sentry in tests
                ["Resend:ApiKey"] = "",
                ["Sentry:Dsn"] = "",
            });
        });

        builder.UseEnvironment("Testing");
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
