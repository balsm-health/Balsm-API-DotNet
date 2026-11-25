using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using static Balsm.API.IntegrationTests.FamilyLinkApiDriver;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// Threat E10 (IDOR, blocking): a third account that is not a party to a link
/// gets 404 on every family-link route — the same answer as an id that does not
/// exist — and every route refuses an anonymous caller.
/// </summary>
[Collection(nameof(LiteWebAppCollection))]
public sealed class FamilyLinkIdorEndpointTests(LiteWebAppFactory factory)
{
    private readonly FamilyLinkApiDriver _api = new(factory);

    public static TheoryData<string, string> LinkRoutes => new()
    {
        { "GET", "" },
        { "POST", "/approve" },
        { "POST", "/decline" },
        { "DELETE", "" },
        { "PUT", "/keys" },
        { "PUT", "/card" },
        { "GET", "/card" },
        { "DELETE", "/card" },
    };

    private static HttpRequestMessage Request(string method, string url) => new(new HttpMethod(method), url)
    {
        Content = method == "PUT"
            ? JsonContent.Create(new
            {
                public_key = Convert.ToBase64String(Key(3)),
                version = 9,
                ephemeral_public_key = Convert.ToBase64String(Key(3)),
                nonce = Convert.ToBase64String(new byte[12]),
                ciphertext = Convert.ToBase64String(new byte[16]),
            })
            : null,
    };

    [Theory]
    [MemberData(nameof(LinkRoutes))]
    public async Task EveryRoute_ForAThirdAccount_Answers404LikeAnUnknownId(string method, string suffix)
    {
        var a = await _api.NewPatientAsync();
        var b = await _api.NewPatientAsync();
        var c = await _api.NewPatientAsync();
        var id = await _api.LinkAsync(a, b);
        (await PutCardAsync(b, id, 1)).EnsureSuccessStatusCode();

        var stranger = await c.Client.SendAsync(Request(method, $"{Base}/{id}{suffix}"));
        var unknown = await c.Client.SendAsync(Request(method, $"{Base}/{Guid.NewGuid()}{suffix}"));

        stranger.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.Content.ReadAsStringAsync()).Should().NotContain(a.UserId.ToString()).And.NotContain(b.UserId.ToString());
        (await BodyAsync(stranger))["error"]!["code"]!.ToString()
            .Should().Be((await BodyAsync(unknown))["error"]!["code"]!.ToString());
    }

    [Fact]
    public async Task Lists_ForAThirdAccount_AreEmpty()
    {
        var a = await _api.NewPatientAsync();
        var b = await _api.NewPatientAsync();
        var c = await _api.NewPatientAsync();
        await _api.RequestAsync(a, b);

        var incoming = (await BodyAsync(await c.Client.GetAsync($"{Base}/incoming")))["data"]!;
        var outgoing = (await BodyAsync(await c.Client.GetAsync($"{Base}/outgoing")))["data"]!;

        (incoming["total_count"]!.GetValue<int>(), outgoing["total_count"]!.GetValue<int>()).Should().Be((0, 0));
    }

    [Theory]
    [MemberData(nameof(LinkRoutes))]
    public async Task EveryRoute_WithoutAToken_IsRejected(string method, string suffix)
    {
        var response = await factory.CreateClient().SendAsync(Request(method, $"{Base}/{Guid.NewGuid()}{suffix}"));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("POST", "")]
    [InlineData("GET", "/incoming")]
    [InlineData("GET", "/outgoing")]
    public async Task CollectionRoutes_WithoutAToken_AreRejected(string method, string suffix)
    {
        var response = await factory.CreateClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), $"{Base}{suffix}")
        {
            Content = method == "POST" ? JsonContent.Create(new { id = Guid.NewGuid(), jti = Guid.NewGuid(), relation = "son" }) : null,
        });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }
}
