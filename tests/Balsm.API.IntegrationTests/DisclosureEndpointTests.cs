using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary><c>POST /disclosure/accept</c> over real HTTP.</summary>
[Collection(nameof(LiteWebAppCollection))]
public sealed class DisclosureEndpointTests(LiteWebAppFactory factory)
{
    [Fact]
    public async Task Accept_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/disclosure/accept", Body("v1"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Accept_FirstTime_RecordsANewAcceptance()
    {
        var user = await TestAuth.SignInAsync(factory, "disclosure-first@example.test");
        var client = TestAuth.CreateClient(factory, user.AccessToken);

        var response = await client.PostAsJsonAsync("/disclosure/accept", Body("v1"));

        (await TestAuth.ReadDataAsync(response)).GetProperty("already_accepted").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Accept_SameVersionTwice_ReturnsTheFirstAcceptance()
    {
        var user = await TestAuth.SignInAsync(factory, "disclosure-twice@example.test");
        var client = TestAuth.CreateClient(factory, user.AccessToken);
        var first = await TestAuth.ReadDataAsync(await client.PostAsJsonAsync("/disclosure/accept", Body("v2")));

        var second = await TestAuth.ReadDataAsync(await client.PostAsJsonAsync("/disclosure/accept", Body("v2")));

        second.GetProperty("already_accepted").GetBoolean().Should().BeTrue();
        second.GetProperty("acceptance_id").GetGuid().Should().Be(first.GetProperty("acceptance_id").GetGuid());
    }

    private static Dictionary<string, string> Body(string version) => new()
    {
        ["disclosure_id"] = "cross-border-transfer",
        ["version"] = version,
        ["country_code"] = "EG",
        ["supervisory_authority"] = "Test Authority",
        ["language"] = "en",
    };
}
