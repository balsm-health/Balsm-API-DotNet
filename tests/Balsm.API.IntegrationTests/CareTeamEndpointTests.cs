using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// Care-team endpoints over real HTTP through the host.
///
/// These exist to catch what the handler and controller unit tests cannot: a
/// module whose controllers were never registered as an ApplicationPart (route
/// answers 404 instead of 401), a route template that collides with another
/// module's, and — most importantly — an endpoint carrying PHI that has fallen
/// outside the auth middleware. The last one is the reason this file is not
/// optional: every one of these routes reads or writes patient health data.
/// </summary>
[Collection(nameof(LiteWebAppCollection))]
public sealed class CareTeamEndpointTests(LiteWebAppFactory factory)
{
    private static readonly Guid ProviderId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ProfileId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Pull_WithoutToken_IsRejected()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/care-team/providers?health_profile_id={ProfileId}");

        // 401/403 both acceptable; 404 would mean the route never registered and
        // 200 would mean a PHI feed is readable anonymously.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upsert_WithoutToken_IsRejected()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/care-team/providers", new
        {
            id = ProviderId,
            health_profile_id = ProfileId,
            type = "doctor",
            name = "Provider Alpha",
            created_at = "2026-01-01T00:00:00Z"
        });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_WithoutToken_IsRejected()
    {
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/care-team/providers/{ProviderId}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Health_IsReachableWhileTheDataRoutesAreNot()
    {
        // Proves the module's controllers really are registered — otherwise the
        // rejections above could be a 404 dressed up as an auth failure.
        var client = factory.CreateClient();

        var health = await client.GetAsync("/care-team/health");

        health.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
