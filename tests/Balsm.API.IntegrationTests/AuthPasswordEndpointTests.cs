using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// The password routes of <c>AuthController</c> over real HTTP:
/// <c>/auth/password</c> (set), <c>/auth/password/sign-in</c> and
/// <c>/auth/password/reset</c>, with their 400/401/423 mappings.
/// </summary>
[Collection(nameof(AuthWebAppCollection))]
public sealed class AuthPasswordEndpointTests(LiteWebAppFactory factory)
{
    private const string Password = "correct-horse-battery";

    [Fact]
    public async Task SetPassword_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/password", new Dictionary<string, string> { ["password"] = Password });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetPassword_SignedIn_ReturnsPasswordSet()
    {
        var user = await TestAuth.SignInAsync(factory, "set-password@example.test");
        var client = TestAuth.CreateClient(factory, user.AccessToken);

        var response = await client.PostAsJsonAsync("/auth/password", new Dictionary<string, string> { ["password"] = Password });

        (await TestAuth.ReadDataAsync(response)).GetProperty("password_set").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task SetPassword_TooShort_Returns400()
    {
        var user = await TestAuth.SignInAsync(factory, "set-short@example.test");
        var client = TestAuth.CreateClient(factory, user.AccessToken);

        var response = await client.PostAsJsonAsync("/auth/password", new Dictionary<string, string> { ["password"] = "short" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PasswordSignIn_CorrectPassword_ReturnsReturningUserSession()
    {
        const string email = "sign-in-ok@example.test";
        await SignUpWithPasswordAsync(email);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/password/sign-in", SignInBody(email, Password));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TestAuth.ReadDataAsync(response)).GetProperty("is_new_user").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task PasswordSignIn_WrongPassword_Returns401InvalidCredentials()
    {
        const string email = "sign-in-wrong@example.test";
        await SignUpWithPasswordAsync(email);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/password/sign-in", SignInBody(email, "not-the-password"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await TestAuth.ReadErrorCodeAsync(response)).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PasswordSignIn_LockedAddress_Returns423()
    {
        const string email = "sign-in-locked@example.test";
        await TestAuth.LockAsync(factory, email);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/password/sign-in", SignInBody(email, Password));

        response.StatusCode.Should().Be(HttpStatusCode.Locked);
    }

    [Fact]
    public async Task ResetPassword_ValidCode_ReturnsPasswordReset()
    {
        const string email = "reset-ok@example.test";
        await TestAuth.SignInAsync(factory, email);
        await TestAuth.SeedChallengeAsync(factory, email);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/password/reset", ResetBody(email, TestAuth.Code));

        (await TestAuth.ReadDataAsync(response)).GetProperty("password_reset").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ResetPassword_WrongCode_Returns401()
    {
        const string email = "reset-wrong@example.test";
        await TestAuth.SignInAsync(factory, email);
        await TestAuth.SeedChallengeAsync(factory, email);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/password/reset", ResetBody(email, "000000"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResetPassword_LockedAddress_Returns423()
    {
        const string email = "reset-locked@example.test";
        await TestAuth.LockAsync(factory, email);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/password/reset", ResetBody(email, TestAuth.Code));

        response.StatusCode.Should().Be(HttpStatusCode.Locked);
    }

    private async Task SignUpWithPasswordAsync(string email)
    {
        var user = await TestAuth.SignInAsync(factory, email);
        var set = await TestAuth.CreateClient(factory, user.AccessToken)
            .PostAsJsonAsync("/auth/password", new Dictionary<string, string> { ["password"] = Password });
        set.EnsureSuccessStatusCode();
    }

    private static Dictionary<string, object> SignInBody(string email, string password) => new()
    {
        ["email"] = email,
        ["password"] = password,
        ["device_id"] = TestAuth.DeviceId,
        ["device_label"] = "Test device",
    };

    private static Dictionary<string, string> ResetBody(string email, string code) => new()
    {
        ["email"] = email,
        ["code"] = code,
        ["new_password"] = "a-new-password-1",
    };
}
