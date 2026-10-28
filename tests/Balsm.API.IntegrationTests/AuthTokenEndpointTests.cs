using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// The session routes of <c>AuthController</c> over real HTTP:
/// <c>/auth/refresh</c>, <c>/auth/sign-out</c> and <c>/auth/recovery/claim</c>.
/// </summary>
[Collection(nameof(AuthWebAppCollection))]
public sealed class AuthTokenEndpointTests(LiteWebAppFactory factory)
{
    private const string RecoverySecret = "test-recovery-secret";

    [Fact]
    public async Task Refresh_ActiveToken_ReturnsANewPair()
    {
        var user = await TestAuth.SignInAsync(factory, "refresh-ok@example.test");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/refresh", RefreshBody(user.RefreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TestAuth.ReadDataAsync(response)).GetProperty("refresh_token").GetString().Should().NotBe(user.RefreshToken);
    }

    [Fact]
    public async Task Refresh_UnknownToken_Returns401TokenNotFound()
    {
        var client = factory.CreateClient();
        var unknown = Convert.ToBase64String(new byte[64]);

        var response = await client.PostAsJsonAsync("/auth/refresh", RefreshBody(unknown));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await TestAuth.ReadErrorCodeAsync(response)).Should().Be("TokenNotFound");
    }

    [Fact]
    public async Task Refresh_AlreadyRotatedToken_Returns401TokenRevoked()
    {
        var user = await TestAuth.SignInAsync(factory, "refresh-reuse@example.test");
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/auth/refresh", RefreshBody(user.RefreshToken))).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/auth/refresh", RefreshBody(user.RefreshToken));

        (await TestAuth.ReadErrorCodeAsync(response)).Should().Be("TokenRevoked");
    }

    [Fact]
    public async Task SignOut_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/sign-out", SignOutBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignOut_SignedIn_RevokesTheDeviceRefreshToken()
    {
        var user = await TestAuth.SignInAsync(factory, "sign-out@example.test");
        var client = TestAuth.CreateClient(factory, user.AccessToken);

        var response = await client.PostAsJsonAsync("/auth/sign-out", SignOutBody());

        (await TestAuth.ReadDataAsync(response)).GetProperty("signed_out").GetBoolean().Should().BeTrue();
        var refresh = await factory.CreateClient().PostAsJsonAsync("/auth/refresh", RefreshBody(user.RefreshToken));
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RecoveryClaim_ValidSupportTokenForKnownIdentity_ReturnsSession()
    {
        const string email = "recovery-ok@example.test";
        var user = await TestAuth.SignInAsync(factory, email);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/recovery/claim", RecoveryBody(email, SupportToken(email)));

        (await TestAuth.ReadDataAsync(response)).GetProperty("user_id").GetGuid().Should().Be(user.UserId);
    }

    [Fact]
    public async Task RecoveryClaim_ForgedSupportToken_Returns403InvalidSupportToken()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/recovery/claim", RecoveryBody("recovery-forged@example.test", "0:deadbeef"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await TestAuth.ReadErrorCodeAsync(response)).Should().Be("InvalidSupportToken");
    }

    [Fact]
    public async Task RecoveryClaim_ValidSupportTokenForUnknownIdentity_Returns404IdentityNotFound()
    {
        const string email = "recovery-unknown@example.test";
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/recovery/claim", RecoveryBody(email, SupportToken(email)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await TestAuth.ReadErrorCodeAsync(response)).Should().Be("IdentityNotFound");
    }

    // Mirrors the support runbook: "{unix}:{HMAC-SHA256(email:unix, Recovery:Secret)}".
    private static string SupportToken(string email)
    {
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(RecoverySecret), Encoding.UTF8.GetBytes($"{email}:{unix}"));
        return $"{unix}:{Convert.ToHexString(mac)}";
    }

    private static Dictionary<string, object> RefreshBody(string refreshToken) => new()
    {
        ["refresh_token"] = refreshToken,
        ["device_id"] = TestAuth.DeviceId,
    };

    private static Dictionary<string, object> SignOutBody() => new() { ["device_id"] = TestAuth.DeviceId };

    private static Dictionary<string, object> RecoveryBody(string email, string supportToken) => new()
    {
        ["email"] = email,
        ["support_token"] = supportToken,
        ["device_id"] = TestAuth.DeviceId,
        ["device_label"] = "Test device",
    };
}
