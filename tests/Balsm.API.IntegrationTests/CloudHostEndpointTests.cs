using System.Net;
using System.Text.Json;
using Balsm.SharedKernel.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// The host as it runs in the cloud (<c>DeploymentMode=Cloud</c>), over real
/// HTTP: behind a TLS-terminating proxy, without the admin portal.
/// </summary>
[Collection(nameof(CloudWebAppCollection))]
public sealed class CloudHostEndpointTests(CloudWebAppFactory factory)
{
    [Fact]
    public async Task Version_ReportsCloudMode()
    {
        var response = await ForwardedHttpsClient().GetAsync("/api/v1/version");

        (await ReadJsonAsync(response)).GetProperty("deployment_mode").GetString().Should().Be("Cloud");
    }

    [Fact]
    public async Task Request_ForwardedAsHttps_IsServedWithoutARedirect()
    {
        var response = await ForwardedHttpsClient().GetAsync("/api/v1/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminPanelRoute_DoesNotExist()
    {
        var response = await ForwardedHttpsClient().GetAsync("/settings/backups");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminPortalRoutes_DoNotExist()
    {
        var response = await ForwardedHttpsClient().GetAsync("/api/v1/admin/auth/status");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DebugTracing_RequestWithBody_IsStillAnswered()
    {
        using var content = new StringContent("""{"email":"trace@example.test"}""", System.Text.Encoding.UTF8, "application/json");

        var response = await ForwardedHttpsClient().PostAsync("/api/v1/version?code=123456", content);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task FirstRunOrchestrator_SeedWorkspace_IsReportedByServerInfo()
    {
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IFirstRunOrchestrator>()
                .SeedWorkspaceAsync("Cloud Test Clinic", "cloud-test-clinic", "en");
        }

        var response = await ForwardedHttpsClient().GetAsync("/api/v1/server-info");

        (await ReadJsonAsync(response)).GetProperty("workspace_name").GetString().Should().Be("Cloud Test Clinic");
    }

    private HttpClient ForwardedHttpsClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        return client;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}
