using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// The host's own surface over real HTTP on the default (admin portal on)
/// host: the platform controllers, the OpenAPI documents, CORS, and the admin
/// SPA fallback that Program.cs wires up.
/// </summary>
[Collection(nameof(LiteWebAppCollection))]
public sealed class PlatformEndpointTests(LiteWebAppFactory factory)
{
    [Fact]
    public async Task Version_Anonymous_ReportsTheDeploymentMode()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/version");

        (await ReadJsonAsync(response)).GetProperty("deployment_mode").GetString().Should().Be("Standalone");
    }

    [Theory]
    [InlineData("/api/v1/admin/audit/logs")]
    [InlineData("/api/v1/admin/backups")]
    [InlineData("/api/v1/admin/logs/files")]
    public async Task AdminRoute_Anonymous_IsRefused(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("identity", "Balsm Identity API")]
    [InlineData("supervisor", "Balsm Supervisor API")]
    [InlineData("all", "Balsm API (aggregate)")]
    public async Task OpenApiDocument_ReturnsTheModuleSpec(string document, string title)
    {
        var response = await factory.CreateClient().GetAsync($"/openapi/v1/{document}.json");

        (await ReadJsonAsync(response)).GetProperty("info").GetProperty("title").GetString().Should().Be(title);
    }

    [Fact]
    public async Task OpenApiDocument_ModuleSpec_ListsOnlyThatModulesRoutes()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1/identity.json");

        (await ReadJsonAsync(response)).GetProperty("paths").EnumerateObject().Select(p => p.Name)
            .Should().OnlyContain(path => path.StartsWith("/api/v1/admin/users") || path == "/identity/health");
    }

    [Fact]
    public async Task Cors_ListedOrigin_IsAllowed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/version");
        request.Headers.Add("Origin", "https://app.balsm.health");

        var response = await factory.CreateClient().SendAsync(request);

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("https://app.balsm.health");
    }

    [Fact]
    public async Task Cors_UnlistedOrigin_IsNotAllowed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/version");
        request.Headers.Add("Origin", "https://evil.example.test");

        var response = await factory.CreateClient().SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task SpaFallback_UnknownPanelRoute_ServesTheAdminIndex()
    {
        var response = await factory.CreateClient().GetAsync("/settings/backups");

        (response.StatusCode, response.Content.Headers.ContentType?.MediaType)
            .Should().Be((HttpStatusCode.OK, "text/html"));
    }

    [Fact]
    public async Task SpaFallback_UnknownPanelRoute_IsNeverCached()
    {
        var response = await factory.CreateClient().GetAsync("/settings/backups");

        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task SpaAssetPath_IsCachedAsImmutable()
    {
        var response = await factory.CreateClient().GetAsync("/assets/not-built.js");

        response.Headers.CacheControl!.ToString().Should().Contain("immutable");
    }

    [Theory]
    [InlineData("/api/v1/does-not-exist")]
    [InlineData("/openapi/v1/missing/extra")]
    [InlineData("/connect/unknown")]
    public async Task SpaFallback_ReservedApiPath_Returns404(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
}
