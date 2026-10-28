using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// <c>AccountController</c> (<c>/account/*</c>) over real HTTP, signed in
/// through the API's own OTP verify so the account row exists as it would for
/// a real patient.
/// </summary>
[Collection(nameof(LiteWebAppCollection))]
public sealed class AccountEndpointTests(LiteWebAppFactory factory)
{
    [Fact]
    public async Task GetSelf_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/account/self");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSelf_SignedIn_ReturnsTheCallersAccount()
    {
        var user = await TestAuth.SignInAsync(factory, "account-self@example.test");
        var client = TestAuth.CreateClient(factory, user.AccessToken);

        var response = await client.GetAsync("/account/self");

        (await TestAuth.ReadDataAsync(response)).GetProperty("user_id").GetGuid().Should().Be(user.UserId);
    }

    [Fact]
    public async Task CheckHandle_FreeHandle_ReturnsAvailable()
    {
        var client = await SignedInClientAsync("account-check@example.test");

        var response = await client.PostAsJsonAsync("/account/handle/check", Handle("free_handle_one"));

        (await TestAuth.ReadDataAsync(response)).GetProperty("available").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ClaimHandle_FreeHandle_ReturnsTheClaimedHandle()
    {
        var client = await SignedInClientAsync("account-claim@example.test");

        var response = await client.PostAsJsonAsync("/account/handle/claim", Handle("claimed_handle"));

        (await TestAuth.ReadDataAsync(response)).GetProperty("handle").GetString().Should().Be("claimed_handle");
    }

    [Fact]
    public async Task ClaimHandle_TakenHandle_Returns409HandleTaken()
    {
        var first = await SignedInClientAsync("account-taken-1@example.test");
        (await first.PostAsJsonAsync("/account/handle/claim", Handle("taken_handle"))).EnsureSuccessStatusCode();
        var second = await SignedInClientAsync("account-taken-2@example.test");

        var response = await second.PostAsJsonAsync("/account/handle/claim", Handle("taken_handle"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await TestAuth.ReadErrorCodeAsync(response)).Should().Be("HandleTaken");
    }

    [Fact]
    public async Task SetDob_Adult_ReturnsSet()
    {
        var client = await SignedInClientAsync("account-dob-adult@example.test");

        var response = await client.PostAsJsonAsync("/account/dob", new Dictionary<string, string> { ["date_of_birth"] = "1990-01-01" });

        (await TestAuth.ReadDataAsync(response)).GetProperty("set").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task SetDob_Minor_Returns422UnderEighteen()
    {
        var client = await SignedInClientAsync("account-dob-minor@example.test");

        var response = await client.PostAsJsonAsync("/account/dob", new Dictionary<string, string> { ["date_of_birth"] = "2020-01-01" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdateProfile_ValidFields_ReturnsUpdated()
    {
        var client = await SignedInClientAsync("account-profile@example.test");
        var body = new Dictionary<string, string>
        {
            ["first_name"] = "Jane",
            ["last_name"] = "Doe",
            ["date_of_birth"] = "1990-01-01",
            ["national_id"] = "00000000000000",
        };

        var response = await client.PatchAsJsonAsync("/account/profile", body);

        (await TestAuth.ReadDataAsync(response)).GetProperty("updated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UpdateProfile_MinorDob_Returns422UnderEighteen()
    {
        var client = await SignedInClientAsync("account-profile-minor@example.test");

        var response = await client.PatchAsJsonAsync("/account/profile", new Dictionary<string, string> { ["date_of_birth"] = "2020-01-01" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ChangeCountry_SignedIn_EchoesTheNewCountry()
    {
        var client = await SignedInClientAsync("account-country@example.test");

        var response = await client.PatchAsJsonAsync("/account/country", new Dictionary<string, string> { ["country_code"] = "SA" });

        (await TestAuth.ReadDataAsync(response)).GetProperty("country_code").GetString().Should().Be("SA");
    }

    [Fact]
    public async Task ChangeLanguage_SignedIn_EchoesTheNewLanguage()
    {
        var client = await SignedInClientAsync("account-language@example.test");

        var response = await client.PatchAsJsonAsync("/account/language", new Dictionary<string, string> { ["language"] = "en" });

        (await TestAuth.ReadDataAsync(response)).GetProperty("preferred_language").GetString().Should().Be("en");
    }

    private async Task<HttpClient> SignedInClientAsync(string email)
    {
        var user = await TestAuth.SignInAsync(factory, email);
        return TestAuth.CreateClient(factory, user.AccessToken);
    }

    private static Dictionary<string, string> Handle(string handle) => new() { ["handle"] = handle };
}
