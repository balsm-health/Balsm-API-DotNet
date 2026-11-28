using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// <c>POST /auth/apple/notifications</c>: Apple's server-to-server events. The
/// signed JWT is the only authentication, so the validator's JWKS is swapped for
/// a test EC key and the tests sign their own payloads with it.
/// </summary>
public sealed class AppleNotificationsEndpointTests(LiteWebAppFactory factory)
    : IClassFixture<LiteWebAppFactory>
{
    private const string ClientId = "app.balsm.test";
    private static readonly ECDsa TestKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private WebApplicationFactory<Program> Host(bool enabled) =>
        factory.WithWebHostBuilder(b =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["SocialSignIn:Enabled"] = enabled ? "true" : "false",
                ["Google:ClientId"] = "test.apps.googleusercontent.com",
                ["Apple:ClientId"] = ClientId,
            };
            foreach (var (key, value) in settings) b.UseSetting(key, value);
            b.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<AppleOidcValidator>();
                services.AddScoped<AppleOidcValidator, TestKeyAppleOidcValidator>();
            });
        });

    [Fact]
    public async Task Post_ValidSignedPayload_Returns200()
    {
        using var host = Host(enabled: true);

        var response = await host.CreateClient().PostAsJsonAsync(
            "/auth/apple/notifications", new { payload = Sign(ClientId, DateTime.UtcNow.AddMinutes(5)) });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_WrongAudience_Returns400()
    {
        using var host = Host(enabled: true);

        var response = await host.CreateClient().PostAsJsonAsync(
            "/auth/apple/notifications", new { payload = Sign("someone.else", DateTime.UtcNow.AddMinutes(5)) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Expired_Returns400()
    {
        using var host = Host(enabled: true);

        var response = await host.CreateClient().PostAsJsonAsync(
            "/auth/apple/notifications", new { payload = Sign(ClientId, DateTime.UtcNow.AddHours(-1)) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_MissingPayload_Returns400()
    {
        using var host = Host(enabled: true);

        var response = await host.CreateClient().PostAsJsonAsync("/auth/apple/notifications", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_WhenDisabled_Returns404()
    {
        using var host = Host(enabled: false);

        var response = await host.CreateClient().PostAsJsonAsync(
            "/auth/apple/notifications", new { payload = Sign(ClientId, DateTime.UtcNow.AddMinutes(5)) });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string Sign(string audience, DateTime expires)
    {
        var events = JsonSerializer.Serialize(new
        {
            type = "consent-revoked",
            sub = "001234.unknown-sub.0001",
            event_time = 1735689600000,
        });
        var token = new JwtSecurityToken(
            issuer: "https://appleid.apple.com",
            audience: audience,
            claims: [new Claim("events", events), new Claim("jti", "jti-1")],
            notBefore: expires.AddHours(-2),
            expires: expires,
            signingCredentials: new SigningCredentials(
                new ECDsaSecurityKey(TestKey) { KeyId = "test-ec" }, SecurityAlgorithms.EcdsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class TestKeyAppleOidcValidator(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        HybridCache cache,
        ILogger<AppleOidcValidator> logger)
        : AppleOidcValidator(configuration, httpClientFactory, cache, logger)
    {
        protected override Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(CancellationToken ct) =>
            Task.FromResult<IEnumerable<SecurityKey>>([new ECDsaSecurityKey(TestKey) { KeyId = "test-ec" }]);
    }
}
