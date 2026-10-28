using System.Net;
using System.Net.Http.Json;
using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task Pull_AfterUpsert_ReturnsTheCallersDecryptedRow()
    {
        var client = ClientFor(Guid.NewGuid());
        var id = Guid.NewGuid();
        await client.PostAsJsonAsync("/care-team/providers", ProviderBody(id, "doctor"));

        var response = await client.GetAsync($"/care-team/providers?health_profile_id={ProfileId}");

        var row = (await TestAuth.ReadDataAsync(response)).EnumerateArray().Single();
        row.GetProperty("id").GetGuid().Should().Be(id);
        row.GetProperty("name").GetString().Should().Be("Dr. Jane Doe");
        row.GetProperty("is_deleted").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Pull_SinceAfterTheLastChange_ReturnsNoRows()
    {
        var client = ClientFor(Guid.NewGuid());
        await client.PostAsJsonAsync("/care-team/providers", ProviderBody(Guid.NewGuid(), "nurse"));

        var response = await client.GetAsync(
            $"/care-team/providers?health_profile_id={ProfileId}&since={DateTime.UtcNow.AddDays(1):O}");

        (await TestAuth.ReadDataAsync(response)).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Upsert_UnknownType_Returns422()
    {
        var client = ClientFor(Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/care-team/providers", ProviderBody(Guid.NewGuid(), "wizard"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Upsert_RowOwnedByAnotherUser_Returns404()
    {
        var id = Guid.NewGuid();
        await ClientFor(Guid.NewGuid()).PostAsJsonAsync("/care-team/providers", ProviderBody(id, "doctor"));

        var response = await ClientFor(Guid.NewGuid()).PostAsJsonAsync("/care-team/providers", ProviderBody(id, "doctor"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upsert_TombstonedRow_Returns409()
    {
        var client = ClientFor(Guid.NewGuid());
        var id = Guid.NewGuid();
        await client.PostAsJsonAsync("/care-team/providers", ProviderBody(id, "doctor"));
        await client.DeleteAsync($"/care-team/providers/{id}");

        var response = await client.PostAsJsonAsync("/care-team/providers", ProviderBody(id, "doctor"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_OwnRow_Returns204()
    {
        var client = ClientFor(Guid.NewGuid());
        var id = Guid.NewGuid();
        await client.PostAsJsonAsync("/care-team/providers", ProviderBody(id, "lab"));

        var response = await client.DeleteAsync($"/care-team/providers/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_UnknownRow_Returns404()
    {
        var response = await ClientFor(Guid.NewGuid()).DeleteAsync($"/care-team/providers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private HttpClient ClientFor(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<JwtService>()
            .IssueAccessToken(userId, $"care-team-{userId:N}@example.test");
        return TestAuth.CreateClient(factory, token);
    }

    private static Dictionary<string, object?> ProviderBody(Guid id, string type) => new()
    {
        ["id"] = id,
        ["health_profile_id"] = ProfileId,
        ["type"] = type,
        ["name"] = "Dr. Jane Doe",
        ["specialty"] = "General practice",
        ["phone"] = "+20 100 000 0000",
        ["email"] = "jane.doe@example.test",
        ["created_at"] = "2026-01-01T00:00:00Z",
    };
}
