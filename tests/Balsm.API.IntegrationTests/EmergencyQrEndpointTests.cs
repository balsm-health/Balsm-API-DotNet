using System.Net;
using System.Net.Http.Json;
using System.Text;
using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// <c>EmergencyQrController</c> (<c>/emergency-qr/*</c>) over real HTTP.
///
/// The server only ever stores client-encrypted ciphertext, so every payload
/// here is an obviously synthetic byte string — no medical data of any kind.
/// Each test signs in a fresh synthetic user id, so the per-user "one active
/// token" rule never couples two tests together.
/// </summary>
[Collection(nameof(LiteWebAppCollection))]
public sealed class EmergencyQrEndpointTests(LiteWebAppFactory factory)
{
    private static readonly byte[] SyntheticCiphertext = Encoding.UTF8.GetBytes("synthetic-test-ciphertext-v1");
    private static readonly byte[] RefreshedCiphertext = Encoding.UTF8.GetBytes("synthetic-test-ciphertext-v2");

    [Fact]
    public async Task Mint_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/emergency-qr/mint", MintBody(3600));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Mint_TimedTtl_ReturnsTokenIdAndExpiry()
    {
        var client = ClientFor(Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/emergency-qr/mint", MintBody(3600));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = await TestAuth.ReadDataAsync(response);
        data.GetProperty("token_id").GetGuid().Should().NotBeEmpty();
        data.GetProperty("expires_at").GetDateTime().Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Mint_DisallowedTtl_Returns422InvalidTtl()
    {
        var client = ClientFor(Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/emergency-qr/mint", MintBody(42));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await TestAuth.ReadErrorCodeAsync(response)).Should().Be("EmergencyQr.InvalidTtl");
    }

    [Fact]
    public async Task Mint_SameClientTokenIdTwice_ReturnsTheSameToken()
    {
        var client = ClientFor(Guid.NewGuid());
        var tokenId = Guid.NewGuid();
        await client.PostAsJsonAsync("/emergency-qr/mint", MintBody(0, tokenId));

        var retry = await client.PostAsJsonAsync("/emergency-qr/mint", MintBody(0, tokenId, RefreshedCiphertext));

        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TestAuth.ReadDataAsync(retry)).GetProperty("token_id").GetGuid().Should().Be(tokenId);
    }

    [Fact]
    public async Task Mint_TokenIdOwnedByAnotherUser_Returns404()
    {
        var tokenId = Guid.NewGuid();
        await ClientFor(Guid.NewGuid()).PostAsJsonAsync("/emergency-qr/mint", MintBody(0, tokenId));

        var response = await ClientFor(Guid.NewGuid()).PostAsJsonAsync("/emergency-qr/mint", MintBody(0, tokenId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Revoke_OwnToken_ReturnsRevoked()
    {
        var client = ClientFor(Guid.NewGuid());
        var tokenId = await MintAsync(client);

        var response = await client.PostAsync($"/emergency-qr/{tokenId}/revoke", content: null);

        (await TestAuth.ReadDataAsync(response)).GetProperty("revoked").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Revoke_UnknownToken_Returns404()
    {
        var client = ClientFor(Guid.NewGuid());

        var response = await client.PostAsync($"/emergency-qr/{Guid.NewGuid()}/revoke", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await TestAuth.ReadErrorCodeAsync(response)).Should().Be("EmergencyQr.NotFound");
    }

    [Fact]
    public async Task UpdateCiphertext_ActiveToken_ReturnsUpdated()
    {
        var client = ClientFor(Guid.NewGuid());
        var tokenId = await MintAsync(client);

        var response = await client.PutAsJsonAsync($"/emergency-qr/{tokenId}/ciphertext", UpdateBody());

        (await TestAuth.ReadDataAsync(response)).GetProperty("updated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UpdateCiphertext_RevokedToken_Returns409TokenInactive()
    {
        var client = ClientFor(Guid.NewGuid());
        var tokenId = await MintAsync(client);
        await client.PostAsync($"/emergency-qr/{tokenId}/revoke", content: null);

        var response = await client.PutAsJsonAsync($"/emergency-qr/{tokenId}/ciphertext", UpdateBody());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await TestAuth.ReadErrorCodeAsync(response)).Should().Be("EmergencyQr.TokenInactive");
    }

    [Fact]
    public async Task Resolve_ActiveToken_ReturnsTheEnvelopeUncached()
    {
        var tokenId = await MintAsync(ClientFor(Guid.NewGuid()));
        var scanner = factory.CreateClient();

        var response = await scanner.GetAsync($"/emergency-qr/resolve/{tokenId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var data = await TestAuth.ReadDataAsync(response);
        data.GetProperty("v").GetInt32().Should().Be(1);
        data.GetProperty("type").GetString().Should().Be("profile");
        Convert.FromBase64String(data.GetProperty("ciphertext_base64").GetString()!).Should().Equal(SyntheticCiphertext);
    }

    [Fact]
    public async Task Resolve_UnknownToken_Returns404()
    {
        var response = await factory.CreateClient().GetAsync($"/emergency-qr/resolve/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await TestAuth.ReadErrorCodeAsync(response)).Should().Be("NotFound");
    }

    [Fact]
    public async Task GetScans_AfterResolvesFromEachClientClass_ListsTheCoarseClientOnly()
    {
        var owner = ClientFor(Guid.NewGuid());
        var tokenId = await MintAsync(owner);
        await ResolveWithUserAgentAsync(tokenId, "Dart/3.5 (dart:io)");
        await ResolveWithUserAgentAsync(tokenId, "Mozilla/5.0 (TestBrowser)");
        await ResolveWithUserAgentAsync(tokenId, null);

        var response = await owner.GetAsync("/emergency-qr/scans");

        var scans = (await TestAuth.ReadDataAsync(response)).GetProperty("scans").EnumerateArray().ToList();
        scans.Select(s => s.GetProperty("client").GetString())
            .Should().BeEquivalentTo(["app", "web", "unknown"]);
        scans.Should().OnlyContain(s => s.GetProperty("token_id").GetGuid() == tokenId);
    }

    [Fact]
    public async Task GetActive_NoTokenMinted_ReturnsNullData()
    {
        var client = ClientFor(Guid.NewGuid());

        var response = await client.GetAsync("/emergency-qr/active");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"data\":null");
    }

    [Fact]
    public async Task GetActive_AfterMint_ReturnsTheActiveToken()
    {
        var client = ClientFor(Guid.NewGuid());
        var tokenId = await MintAsync(client, ttlSeconds: 86400);

        var response = await client.GetAsync("/emergency-qr/active");

        var data = await TestAuth.ReadDataAsync(response);
        data.GetProperty("token_id").GetGuid().Should().Be(tokenId);
        data.GetProperty("ttl_seconds").GetInt32().Should().Be(86400);
    }

    private HttpClient ClientFor(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<JwtService>()
            .IssueAccessToken(userId, $"qr-{userId:N}@example.test");
        return TestAuth.CreateClient(factory, token);
    }

    private static async Task<Guid> MintAsync(HttpClient client, int ttlSeconds = 3600)
    {
        var response = await client.PostAsJsonAsync("/emergency-qr/mint", MintBody(ttlSeconds));
        response.EnsureSuccessStatusCode();
        return (await TestAuth.ReadDataAsync(response)).GetProperty("token_id").GetGuid();
    }

    private async Task ResolveWithUserAgentAsync(Guid tokenId, string? userAgent)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/emergency-qr/resolve/{tokenId}");
        if (userAgent is not null) request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await factory.CreateClient().SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static Dictionary<string, object?> MintBody(int ttlSeconds, Guid? tokenId = null, byte[]? ciphertext = null) => new()
    {
        ["ciphertext"] = ciphertext ?? SyntheticCiphertext,
        ["profile_etag"] = "etag-synthetic-1",
        ["ttl_seconds"] = ttlSeconds,
        ["token_id"] = tokenId,
    };

    private static Dictionary<string, object> UpdateBody() => new()
    {
        ["ciphertext"] = RefreshedCiphertext,
        ["profile_etag"] = "etag-synthetic-2",
    };
}
