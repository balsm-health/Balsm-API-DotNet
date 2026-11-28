using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
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
    private static readonly RSA TestKey = RSA.Create(2048);

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
            "/auth/apple/notifications", new { payload = Sign(ClientId, expires: null) });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_WrongAudience_Returns400()
    {
        using var host = Host(enabled: true);

        var response = await host.CreateClient().PostAsJsonAsync(
            "/auth/apple/notifications", new { payload = Sign("someone.else", expires: null) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Expired_Returns400()
    {
        using var host = Host(enabled: true);

        var response = await host.CreateClient().PostAsJsonAsync(
            "/auth/apple/notifications", new { payload = Sign(ClientId, expires: DateTime.UtcNow.AddHours(-1), issuedAt: DateTime.UtcNow.AddHours(-2)) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Notification_IatOlderThan7Days_Returns400()
    {
        using var host = Host(enabled: true);

        var response = await host.CreateClient().PostAsJsonAsync(
            "/auth/apple/notifications",
            new { payload = Sign(ClientId, expires: null, issuedAt: DateTime.UtcNow.AddDays(-7).AddMinutes(-1)) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Notification_MissingIat_Returns400()
    {
        using var host = Host(enabled: true);

        var response = await host.CreateClient().PostAsJsonAsync(
            "/auth/apple/notifications", new { payload = Sign(ClientId, expires: null, withIat: false) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Notification_IatInTheFuture_Returns400()
    {
        using var host = Host(enabled: true);

        var response = await host.CreateClient().PostAsJsonAsync(
            "/auth/apple/notifications",
            new { payload = Sign(ClientId, expires: null, issuedAt: DateTime.UtcNow.AddMinutes(10)) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_ConsentRevokedForKnownAppleUser_RevokesSessions()
    {
        using var host = Host(enabled: true);
        var client = host.CreateClient();
        // Unique per run: the host's database can outlive the test process.
        var knownSub = $"001234.{Guid.NewGuid():N}.0001";
        Guid userId;
        using (var scope = host.Services.CreateScope())
        {
            var accountDb = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
            var account = UserAccount.Create(countryCode: "EG", preferredLanguage: "en");
            accountDb.UserAccounts.Add(account);
            await accountDb.SaveChangesAsync();
            userId = account.Id;
            var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            authDb.UserIdentities.Add(UserIdentity.Create(userId, "apple", knownSub, null));
            authDb.UserRefreshTokens.Add(UserRefreshToken.Create(userId, $"hash-{knownSub}", Guid.NewGuid(), DateTime.UtcNow.AddDays(30)));
            await authDb.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/auth/apple/notifications", new { payload = Sign(ClientId, expires: null, sub: knownSub) });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var verify = host.Services.CreateScope();
        (await verify.ServiceProvider.GetRequiredService<AuthDbContext>().UserRefreshTokens.AsNoTracking()
            .SingleAsync(t => t.UserId == userId)).RevokedAt.Should().NotBeNull();
        (await verify.ServiceProvider.GetRequiredService<AccountDbContext>().UserAccounts.AsNoTracking()
            .AnyAsync(a => a.Id == userId)).Should().BeTrue();
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
            "/auth/apple/notifications", new { payload = Sign(ClientId, expires: null) });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string Sign(
        string audience,
        DateTime? expires,
        DateTime? issuedAt = default,
        bool withIat = true,
        string sub = "001234.unknown-sub.0001",
        string type = "consent-revoked")
    {
        var events = JsonSerializer.Serialize(new { type, sub, event_time = 1735689600000 });
        var payload = new JwtPayload
        {
            ["iss"] = "https://appleid.apple.com",
            ["aud"] = audience,
            ["jti"] = "jti-1",
            ["events"] = events,
        };
        if (withIat) payload["iat"] = EpochTime.GetIntDate(issuedAt ?? DateTime.UtcNow);
        if (expires is { } exp) payload["exp"] = EpochTime.GetIntDate(exp);
        var header = new JwtHeader(new SigningCredentials(
            new RsaSecurityKey(TestKey) { KeyId = "test-rsa" }, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }

    private sealed class TestKeyAppleOidcValidator(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        HybridCache cache,
        ILogger<AppleOidcValidator> logger)
        : AppleOidcValidator(configuration, httpClientFactory, cache, logger)
    {
        protected override Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(CancellationToken ct) =>
            Task.FromResult<IEnumerable<SecurityKey>>([new RsaSecurityKey(TestKey) { KeyId = "test-rsa" }]);
    }
}
