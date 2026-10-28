using System.Net;
using Balsm.Sessions.Domain.Entities;
using Balsm.Sessions.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary><c>SessionsController</c> (<c>/sessions</c>) over real HTTP.</summary>
[Collection(nameof(LiteWebAppCollection))]
public sealed class SessionsEndpointTests(LiteWebAppFactory factory)
{
    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_SignedIn_ReturnsOnlyTheCallersSessions()
    {
        var user = await TestAuth.SignInAsync(factory, "sessions-list@example.test");
        var own = await SeedSessionAsync(user.UserId);
        await SeedSessionAsync(Guid.Parse("00000000-0000-0000-0000-00000000beef"));
        var client = TestAuth.CreateClient(factory, user.AccessToken);

        var response = await client.GetAsync("/sessions");

        var ids = (await TestAuth.ReadDataAsync(response)).EnumerateArray().Select(s => s.GetProperty("id").GetGuid());
        ids.Should().Equal(own);
    }

    [Fact]
    public async Task Revoke_OwnSession_ReturnsRevoked()
    {
        var user = await TestAuth.SignInAsync(factory, "sessions-revoke@example.test");
        var session = await SeedSessionAsync(user.UserId);
        var client = TestAuth.CreateClient(factory, user.AccessToken);

        var response = await client.DeleteAsync($"/sessions/{session}");

        (await TestAuth.ReadDataAsync(response)).GetProperty("revoked").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Revoke_AnotherUsersSession_IsRejected()
    {
        var user = await TestAuth.SignInAsync(factory, "sessions-foreign@example.test");
        var foreign = await SeedSessionAsync(Guid.Parse("00000000-0000-0000-0000-00000000cafe"));
        var client = TestAuth.CreateClient(factory, user.AccessToken);

        var response = await client.DeleteAsync($"/sessions/{foreign}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokeAll_SignedIn_LeavesNoActiveSessions()
    {
        var user = await TestAuth.SignInAsync(factory, "sessions-revoke-all@example.test");
        await SeedSessionAsync(user.UserId);
        var client = TestAuth.CreateClient(factory, user.AccessToken);

        await client.DeleteAsync("/sessions");

        var remaining = await TestAuth.ReadDataAsync(await client.GetAsync("/sessions"));
        remaining.GetArrayLength().Should().Be(0);
    }

    private async Task<Guid> SeedSessionAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
        var session = ActiveSession.Create(userId, TestAuth.DeviceId, "Test device", "phone", Guid.NewGuid());
        db.ActiveSessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }
}
