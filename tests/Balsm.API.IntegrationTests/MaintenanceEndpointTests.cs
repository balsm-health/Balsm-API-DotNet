using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Balsm.Infrastructure.Maintenance;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// Maintenance mode over real HTTP: the anonymous read the app polls, the
/// super-admin write behind the admin portal's auth, and the block-mode write
/// guard. Uses its own host (not the shared collection) because it changes
/// platform-wide state.
/// </summary>
public sealed class MaintenanceEndpointTests(LiteWebAppFactory factory) : IClassFixture<LiteWebAppFactory>
{
    [Fact]
    public async Task GetMaintenance_Anonymous_ReturnsModeEnvelope()
    {
        var client = await ReadyClientAsync();
        await SetAsync(new MaintenanceChange("readonly", "Back soon", null));

        var response = await client.GetAsync("/api/v1/maintenance");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = body.RootElement.GetProperty("data");
        (data.GetProperty("mode").GetString(), data.GetProperty("message").GetString(),
                data.GetProperty("ends_at").ValueKind)
            .Should().Be(("readonly", "Back soon", JsonValueKind.Null));
    }

    [Fact]
    public async Task PutAdminMaintenance_WithoutAdminSession_IsRefused()
    {
        var client = await ReadyClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/admin/maintenance", new { mode = "block" });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BlockMode_RefusesPatientWrites_ButKeepsHealthAndReadsUp()
    {
        var client = await ReadyClientAsync();
        await SetAsync(new MaintenanceChange("block", null, null));

        var write = await client.PostAsJsonAsync("/auth/password/sign-in", new { email = "jane.doe@example.com" });
        var health = await client.GetAsync("/api/v1/health");
        var flag = await client.GetAsync("/api/v1/maintenance");
        await SetAsync(new MaintenanceChange("off", null, null));

        using var problem = JsonDocument.Parse(await write.Content.ReadAsStringAsync());
        (write.StatusCode, problem.RootElement.GetProperty("code").GetString(), health.StatusCode, flag.StatusCode)
            .Should().Be((HttpStatusCode.ServiceUnavailable, "maintenance.blocked", HttpStatusCode.OK, HttpStatusCode.OK));
    }

    private async Task SetAsync(MaintenanceChange change)
    {
        var service = factory.Services.GetRequiredService<IMaintenanceModeService>();
        var result = await service.SetAsync(change, "test");
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    private async Task<HttpClient> ReadyClientAsync()
    {
        var client = factory.CreateClient();
        for (var i = 0; i < 100; i++)
        {
            using var health = JsonDocument.Parse(await client.GetStringAsync("/api/v1/health"));
            if (health.RootElement.GetProperty("ready").GetBoolean())
                return client;
            await Task.Delay(100);
        }

        throw new TimeoutException("API did not become ready.");
    }
}
