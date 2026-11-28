using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// <c>POST /auth/google</c> and <c>POST /auth/apple</c> create new accounts only
/// while <c>SocialSignIn:Enabled</c> is true, so ops can switch the feature off
/// without a release; an existing Apple/Google identity can still sign in.
/// Each test builds its own host (fresh in-memory rate-limit counters).
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

    private const string RawNonce = "test-nonce";

    private WebApplicationFactory<Program> Host(
        bool enabled, bool fakeProviders = false, bool clientIdsConfigured = true) =>
        factory.WithWebHostBuilder(b =>
        {
            // Both: UseSetting reaches the boot-time audience check, while
            // ConfigureAppConfiguration is layered last so a box-local
            // appsettings.Local.json (which enables the feature on dev
            // machines) cannot override what the test asks for.
            var settings = new Dictionary<string, string?>
            {
                ["SocialSignIn:Enabled"] = enabled ? "true" : "false",
                ["Google:ClientId"] = clientIdsConfigured ? "test.apps.googleusercontent.com" : "",
                ["Apple:ClientId"] = clientIdsConfigured ? "app.balsm.test" : "",
            };
            foreach (var (key, value) in settings) b.UseSetting(key, value);
            b.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
            if (fakeProviders)
            {
                b.ConfigureTestServices(services =>
                {
                    services.RemoveAll<GoogleOidcValidator>();
                    services.AddScoped<GoogleOidcValidator, SubjectIsTokenGoogleValidator>();
                    services.RemoveAll<AppleOidcValidator>();
                    services.AddScoped<AppleOidcValidator, SubjectIsTokenAppleValidator>();
                });
            }
        });

    private static object SignInBody(string idToken) => new
    {
        id_token = idToken,
        device_id = Guid.NewGuid(),
        device_label = "test",
        country_code = "EG",
        nonce = RawNonce,
    };

    private static async Task<Guid> SeedIdentityAsync(WebApplicationFactory<Program> host, string provider, string sub)
    {
        using var scope = host.Services.CreateScope();
        var accountDb = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var account = UserAccount.Create(countryCode: "EG", preferredLanguage: "en");
        accountDb.UserAccounts.Add(account);
        await accountDb.SaveChangesAsync();
        var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        authDb.UserIdentities.Add(UserIdentity.Create(account.Id, provider, sub, null));
        await authDb.SaveChangesAsync();
        return account.Id;
    }

    private static async Task<(int Accounts, bool IdentityExists)> StateAsync(
        WebApplicationFactory<Program> host, string provider, string sub)
    {
        using var scope = host.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<AccountDbContext>().UserAccounts.CountAsync(),
            await scope.ServiceProvider.GetRequiredService<AuthDbContext>().UserIdentities
                .AnyAsync(i => i.Provider == provider && i.ProviderSubject == sub));
    }

    private static async Task<Guid> UserIdOfAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").GetProperty("user_id").GetGuid();
    }

    [Fact]
    public async Task PostGoogle_DisabledKnownSub_SignsIn()
    {
        using var host = Host(enabled: false, fakeProviders: true);
        var sub = $"g-{Guid.NewGuid():N}";
        var userId = await SeedIdentityAsync(host, "google", sub);

        var response = await host.CreateClient().PostAsJsonAsync("/auth/google", SignInBody(sub));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await UserIdOfAsync(response)).Should().Be(userId);
    }

    [Fact]
    public async Task PostGoogle_DisabledUnknownSub_Returns404NoAccountCreated()
    {
        using var host = Host(enabled: false, fakeProviders: true);
        var sub = $"g-{Guid.NewGuid():N}";
        var before = await StateAsync(host, "google", sub);

        var response = await host.CreateClient().PostAsJsonAsync("/auth/google", SignInBody(sub));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await StateAsync(host, "google", sub)).Should().Be((before.Accounts, false));
    }

    [Fact]
    public async Task PostApple_DisabledKnownSub_SignsIn()
    {
        using var host = Host(enabled: false, fakeProviders: true);
        var sub = $"a-{Guid.NewGuid():N}";
        var userId = await SeedIdentityAsync(host, "apple", sub);

        var response = await host.CreateClient().PostAsJsonAsync("/auth/apple", SignInBody(sub));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await UserIdOfAsync(response)).Should().Be(userId);
    }

    [Fact]
    public async Task PostApple_DisabledUnknownSub_Returns404NoAccountCreated()
    {
        using var host = Host(enabled: false, fakeProviders: true);
        var sub = $"a-{Guid.NewGuid():N}";
        var before = await StateAsync(host, "apple", sub);

        var response = await host.CreateClient().PostAsJsonAsync("/auth/apple", SignInBody(sub));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await StateAsync(host, "apple", sub)).Should().Be((before.Accounts, false));
    }

    [Fact]
    public async Task PostApple_DisabledKnownSub_NonceMismatch_Returns401()
    {
        using var host = Host(enabled: false, fakeProviders: true);
        var sub = $"a-{Guid.NewGuid():N}";
        await SeedIdentityAsync(host, "apple", sub);

        var response = await host.CreateClient().PostAsJsonAsync("/auth/apple", new
        {
            id_token = sub,
            device_id = Guid.NewGuid(),
            device_label = "test",
            country_code = "EG",
            nonce = "another-nonce",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostGoogle_EnabledUnknownSub_CreatesAccount()
    {
        using var host = Host(enabled: true, fakeProviders: true);
        var sub = $"g-{Guid.NewGuid():N}";

        var response = await host.CreateClient().PostAsJsonAsync("/auth/google", SignInBody(sub));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StateAsync(host, "google", sub)).IdentityExists.Should().BeTrue();
    }

    [Theory]
    [InlineData("google")]
    [InlineData("apple")]
    public async Task PostSocial_DisabledAndClientIdMissing_Returns404(string provider)
    {
        using var host = Host(enabled: false, clientIdsConfigured: false);

        var response = await host.CreateClient().PostAsJsonAsync($"/auth/{provider}", SignInBody("any"));

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
    public async Task PostApple_WhenCountryCodeOmitted_Returns400CountryCodeRequired()
    {
        using var host = Host(enabled: true);

        var response = await host.CreateClient().PostAsJsonAsync("/auth/apple", new
        {
            id_token = "placeholder",
            device_id = Guid.NewGuid(),
            device_label = "test",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("CountryCodeRequired");
    }

    [Fact]
    public async Task PostApple_21stCallFromOneIpInWindow_Returns429WithRetryAfter()
    {
        // Disabled + unknown subject: every call within the limit is a 404.
        using var host = Host(enabled: false, fakeProviders: true);
        var client = host.CreateClient();
        var body = SignInBody($"a-{Guid.NewGuid():N}");

        for (var i = 0; i < 20; i++)
        {
            var ok = await client.PostAsJsonAsync("/auth/apple", body);
            ok.StatusCode.Should().Be(HttpStatusCode.NotFound, $"call {i + 1} is within the limit");
        }

        var limited = await client.PostAsJsonAsync("/auth/apple", body);

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.Should().NotBeNull();
        using var json = JsonDocument.Parse(await limited.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("RateLimitExceeded");
    }

    /// Treats the ID token as the subject, so tests choose who signs in.
    private sealed class SubjectIsTokenGoogleValidator(IConfiguration configuration, ILogger<GoogleOidcValidator> logger)
        : GoogleOidcValidator(configuration, logger)
    {
        public override Task<GoogleTokenPayload?> ValidateAsync(string idToken, CancellationToken ct) =>
            Task.FromResult<GoogleTokenPayload?>(new GoogleTokenPayload(idToken, null, false));
    }

    /// Treats the ID token as the subject and binds it to <see cref="RawNonce"/>.
    private sealed class SubjectIsTokenAppleValidator(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        HybridCache cache,
        ILogger<AppleOidcValidator> logger)
        : AppleOidcValidator(configuration, httpClientFactory, cache, logger)
    {
        private static readonly string NonceHash =
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(RawNonce))).ToLowerInvariant();

        public override Task<AppleTokenPayload?> ValidateAsync(string idToken, CancellationToken ct) =>
            Task.FromResult<AppleTokenPayload?>(new AppleTokenPayload(idToken, null, false, false, NonceHash));
    }
}
