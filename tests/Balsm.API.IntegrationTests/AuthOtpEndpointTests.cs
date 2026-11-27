using System.Net;
using System.Net.Http.Json;
using Balsm.Infrastructure.RateLimit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// The OTP and magic-link routes of <c>AuthController</c> over real HTTP:
/// <c>/auth/otp/request</c>, <c>/auth/otp/verify</c>, <c>/auth/otp/link</c>
/// and <c>/auth/otp/verify-link</c>, with their 400/401/423/429 mappings.
/// </summary>
[Collection(nameof(AuthWebAppCollection))]
public sealed class AuthOtpEndpointTests(LiteWebAppFactory factory)
{
    private static readonly Guid ClientAccountId = Guid.Parse("01900000-0000-7000-8000-0000000000a1");

    [Fact]
    public async Task RequestOtp_UnknownPurpose_Returns400InvalidPurpose()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/otp/request", RequestBody("purpose@example.test", "bogus"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await TestAuth.ReadErrorCodeAsync(response)).Should().Be("InvalidPurpose");
    }

    [Fact]
    public async Task RequestOtp_ContinueForNewEmail_ReturnsExpiry()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/otp/request", RequestBody("continue@example.test", "continue"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TestAuth.ReadDataAsync(response)).GetProperty("expires_in_seconds").GetInt32().Should().Be(600);
    }

    [Fact]
    public async Task RequestOtp_ResetWithoutLanguageField_FallsBackToAcceptLanguageAndReturns200()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", "ar-EG,en;q=0.8");
        var body = new Dictionary<string, string>
        {
            ["email"] = "reset-unknown@example.test",
            ["country_code"] = "EG",
            ["purpose"] = "reset",
        };

        var response = await client.PostAsJsonAsync("/auth/otp/request", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RequestOtp_EmailOverItsLimit_Returns429WithRetryAfter()
    {
        const string email = "limited@example.test";
        var store = factory.Services.GetRequiredService<IRateLimitStore>();
        for (var i = 0; i < 3; i++)
            await store.TryConsumeAsync($"otp:email:{email}", 3, TimeSpan.FromMinutes(10));
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/otp/request", RequestBody(email, "continue"));

        response.StatusCode.Should().Be((HttpStatusCode)429);
        response.Headers.Contains("Retry-After").Should().BeTrue();
    }

    [Fact]
    public async Task RequestOtp_LockedAddress_Returns423AccountLocked()
    {
        const string email = "locked-request@example.test";
        await TestAuth.LockAsync(factory, email);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/otp/request", RequestBody(email, "continue"));

        response.StatusCode.Should().Be(HttpStatusCode.Locked);
        (await TestAuth.ReadErrorCodeAsync(response)).Should().Be("AccountLocked");
    }

    [Fact]
    public async Task VerifyOtp_ValidCodeForNewEmail_ReturnsNewUserSession()
    {
        const string email = "verify-new@example.test";
        await TestAuth.SeedChallengeAsync(factory, email);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/otp/verify", VerifyBody(email, TestAuth.Code));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TestAuth.ReadDataAsync(response)).GetProperty("is_new_user").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task VerifyOtp_FreeClientAccountId_AdoptsIt()
    {
        const string email = "verify-adopt@example.test";
        await TestAuth.SeedChallengeAsync(factory, email);
        var client = factory.CreateClient();
        var body = VerifyBody(email, TestAuth.Code);
        body["client_account_id"] = ClientAccountId;

        var response = await client.PostAsJsonAsync("/auth/otp/verify", body);

        var data = await TestAuth.ReadDataAsync(response);
        data.GetProperty("adopted_client_id").GetBoolean().Should().BeTrue();
        data.GetProperty("user_id").GetGuid().Should().Be(ClientAccountId);
    }

    [Fact]
    public async Task VerifyOtp_WrongCode_Returns401()
    {
        const string email = "verify-wrong@example.test";
        await TestAuth.SeedChallengeAsync(factory, email);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/otp/verify", VerifyBody(email, "000000"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyOtp_LockedAddress_Returns423WithRetryAfter()
    {
        const string email = "verify-locked@example.test";
        await TestAuth.SeedChallengeAsync(factory, email);
        await TestAuth.LockAsync(factory, email);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/otp/verify", VerifyBody(email, TestAuth.Code));

        response.StatusCode.Should().Be(HttpStatusCode.Locked);
        response.Headers.Contains("Retry-After").Should().BeTrue();
    }

    [Fact]
    public async Task GetOtpLink_NoWebAppUrl_RedirectsToTheNativeSchemeWithPurpose()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/auth/otp/link?t=abc&p=reset");

        response.Headers.Location!.OriginalString.Should().Be("balsm://auth/link?t=abc&p=reset");
    }

    [Fact]
    public async Task GetOtpLink_UnknownPurpose_DropsItFromTheRedirect()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/auth/otp/link?t=abc&p=javascript");

        response.Headers.Location!.OriginalString.Should().Be("balsm://auth/link?t=abc");
    }

    [Fact]
    public async Task GetOtpLink_WebAppUrlConfigured_ServesInterstitialPointingAtTheWebApp()
    {
        var config = factory.Services.GetRequiredService<IConfiguration>();
        var before = config["Otp:WebAppUrl"];
        config["Otp:WebAppUrl"] = "https://app.example.test/";
        HttpResponseMessage response;
        try
        {
            response = await factory.CreateClient().GetAsync("/auth/otp/link?t=abc");
        }
        finally
        {
            config["Otp:WebAppUrl"] = before;
        }

        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("https://app.example.test/auth/link?t=abc");
    }

    [Fact]
    public async Task VerifyLink_ValidToken_ReturnsSession()
    {
        const string email = "link-valid@example.test";
        await TestAuth.SeedChallengeAsync(factory, email, linkToken: "link-token-valid");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/otp/verify-link", LinkBody("link-token-valid"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TestAuth.ReadDataAsync(response)).GetProperty("access_token").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task VerifyLink_UnknownToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/otp/verify-link", LinkBody("link-token-unknown"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // The API reads snake_case JSON, as the apps send it.
    private static Dictionary<string, string> RequestBody(string email, string purpose) => new()
    {
        ["email"] = email,
        ["country_code"] = "EG",
        ["purpose"] = purpose,
        ["language"] = "en",
    };

    private static Dictionary<string, object> VerifyBody(string email, string code) => new()
    {
        ["email"] = email,
        ["code"] = code,
        ["device_id"] = TestAuth.DeviceId,
        ["device_label"] = "Test device",
    };

    private static Dictionary<string, object> LinkBody(string token) => new()
    {
        ["token"] = token,
        ["device_id"] = TestAuth.DeviceId,
        ["device_label"] = "Test device",
    };
}
