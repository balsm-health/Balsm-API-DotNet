using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Balsm.E2E.Tests;

/// <summary>
/// The whole host — every module, the real middleware pipeline, migrations
/// applied on boot — against throwaway SQLite files, driven over HTTP only.
/// </summary>
public sealed class E2EWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _localDb = Path.Combine(Path.GetTempPath(), $"balsm-e2e-{Guid.NewGuid():N}.db");
    private readonly string _cloudDb = Path.Combine(Path.GetTempPath(), $"balsm-e2e-cloud-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting, not ConfigureAppConfiguration: top-level Program reads its
        // configuration before ConfigureAppConfiguration callbacks apply.
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
            ["CareDirectory:ImportOnStartup"] = "false",
        })
        {
            builder.UseSetting(key, value);
        }

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
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
    }
}
