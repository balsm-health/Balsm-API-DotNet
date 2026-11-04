using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// <c>GET /api/v1/content/pregnancy-weeks</c> over real HTTP: auth, the wire
/// shape the app parses, and the ETag / If-None-Match revalidation path.
/// </summary>
[Collection(nameof(LiteWebAppCollection))]
public sealed class ContentEndpointTests(LiteWebAppFactory factory)
{
    private const string Route = "/api/v1/content/pregnancy-weeks";

    private HttpClient AuthenticatedClient()
    {
        using var scope = factory.Services.CreateScope();
        var userId = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
        var token = scope.ServiceProvider.GetRequiredService<JwtService>().IssueAccessToken(userId, "content@example.test");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task PregnancyWeeks_Authenticated_Returns200WithEveryWeekInBothLanguages()
    {
        using var client = AuthenticatedClient();

        var response = await client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var data = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["data"]!;
        data["version"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        foreach (var key in new[] { "source", "disclaimer", "multiples_note" })
        {
            data[key]!["en"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            data[key]!["ar"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        }
        var weeks = data["weeks"]!.AsArray();
        weeks.Select(w => w!["week"]!.GetValue<int>()).Should().Equal(Enumerable.Range(4, 37));
        weeks.Should().AllSatisfy(w =>
        {
            w!["title"]!["en"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            w["title"]!["ar"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            w["body"]!["en"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            w["body"]!["ar"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public async Task PregnancyWeeks_Authenticated_SendsETagAndCacheControl()
    {
        using var client = AuthenticatedClient();

        var response = await client.GetAsync(Route);

        response.Headers.ETag.Should().NotBeNull();
        response.Headers.ETag!.IsWeak.Should().BeFalse();
        response.Headers.CacheControl!.Private.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromDays(1));
    }

    [Fact]
    public async Task PregnancyWeeks_IfNoneMatchCurrentETag_Returns304WithoutBody()
    {
        using var client = AuthenticatedClient();
        var first = await client.GetAsync(Route);
        using var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);

        var second = await client.SendAsync(request);

        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
        second.Headers.ETag.Should().Be(first.Headers.ETag);
        (await second.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task PregnancyWeeks_IfNoneMatchStaleETag_Returns200()
    {
        using var client = AuthenticatedClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"pw-stale\""));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PregnancyWeeks_Anonymous_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
