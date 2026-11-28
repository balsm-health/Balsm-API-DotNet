using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// <c>POST /auth/google</c> and <c>POST /auth/apple</c> are offered only while
/// <c>SocialSignIn:Enabled</c> is true, so ops can switch the feature off
/// without a release. Each test builds its own host (fresh in-memory rate-limit
/// counters).
/// </summary>
public sealed class AuthSocialSignInEndpointTests(LiteWebAppFactory factory)
    : IClassFixture<LiteWebAppFactory>
{
    private static readonly object Body = new
    {
        id_token = "placeholder",
        device_id = Guid.NewGuid(),
        device_label = "test",
        country_code = "EG",
    };

    private WebApplicationFactory<Program> Host(bool enabled) =>
        factory.WithWebHostBuilder(b =>
        {
            // Both: UseSetting reaches the boot-time audience check, while
            // ConfigureAppConfiguration is layered last so a box-local
            // appsettings.Local.json (which enables the feature on dev
            // machines) cannot override what the test asks for.
            var settings = new Dictionary<string, string?>
            {
                ["SocialSignIn:Enabled"] = enabled ? "true" : "false",
                ["Google:ClientId"] = "test.apps.googleusercontent.com",
                ["Apple:ClientId"] = "app.balsm.test",
            };
            foreach (var (key, value) in settings) b.UseSetting(key, value);
            b.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
        });

    [Fact]
    public async Task PostGoogle_WhenDisabled_Returns404()
    {
        using var host = Host(enabled: false);

        var response = await host.CreateClient().PostAsJsonAsync("/auth/google", Body);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostApple_WhenDisabled_Returns404()
    {
        using var host = Host(enabled: false);

        var response = await host.CreateClient().PostAsJsonAsync("/auth/apple", Body);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostApple_WhenCountryCodeBlank_Returns400CountryCodeRequired()
    {
        using var host = Host(enabled: true);

        var response = await host.CreateClient().PostAsJsonAsync("/auth/apple", new
        {
            id_token = "placeholder",
            device_id = Guid.NewGuid(),
            device_label = "test",
            country_code = "",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("CountryCodeRequired");
    }

    [Fact]
    public async Task PostApple_21stCallFromOneIpInWindow_Returns429WithRetryAfter()
    {
        using var host = Host(enabled: false);
        var client = host.CreateClient();

        for (var i = 0; i < 20; i++)
        {
            var ok = await client.PostAsJsonAsync("/auth/apple", Body);
            ok.StatusCode.Should().Be(HttpStatusCode.NotFound, $"call {i + 1} is within the limit");
        }

        var limited = await client.PostAsJsonAsync("/auth/apple", Body);

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.Should().NotBeNull();
        using var json = JsonDocument.Parse(await limited.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("RateLimitExceeded");
    }
}
