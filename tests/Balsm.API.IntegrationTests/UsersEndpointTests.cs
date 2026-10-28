using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Balsm.Identity.Domain;
using Balsm.Identity.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// Identity <c>UsersController</c> (<c>/api/v1/admin/users</c>) over real HTTP,
/// on a host with the admin portal off so the requests reach the controller.
/// The caller is named by the token's <c>name</c> claim.
/// </summary>
[Collection(nameof(AdminPortalOffWebAppCollection))]
public sealed class UsersEndpointTests(AdminPortalOffWebAppFactory factory)
{
    [Fact]
    public async Task Ping_Returns200()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/admin/users/ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMe_KnownAdmin_ReturnsTheirProfile()
    {
        await SeedAdminAsync("admin-get@example.test");
        var client = ClientFor("admin-get@example.test");

        var response = await client.GetAsync("/api/v1/admin/users/me");

        (await response.Content.ReadAsStringAsync()).Should().Contain("admin-get@example.test");
    }

    [Fact]
    public async Task GetMe_UnknownAdmin_Returns404()
    {
        var client = ClientFor("admin-missing@example.test");

        var response = await client.GetAsync("/api/v1/admin/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateMe_KnownAdmin_ReturnsTheNewDisplayName()
    {
        await SeedAdminAsync("admin-update@example.test");
        var client = ClientFor("admin-update@example.test");

        var response = await client.PatchAsJsonAsync(
            "/api/v1/admin/users/me", new Dictionary<string, string> { ["display_name"] = "Renamed Admin" });

        (await response.Content.ReadAsStringAsync()).Should().Contain("Renamed Admin");
    }

    [Fact]
    public async Task UpdateMe_UnknownAdmin_Returns404()
    {
        var client = ClientFor("admin-update-missing@example.test");

        var response = await client.PatchAsJsonAsync(
            "/api/v1/admin/users/me", new Dictionary<string, string> { ["display_name"] = "Nobody", ["locale"] = "ar" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private HttpClient ClientFor(string email) =>
        TestAuth.CreateClient(factory, TestAuth.MintToken(new Claim("unique_name", email)));

    private async Task SeedAdminAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.AdminUsers.Add(AdminUserMirror.Create(email, "Test Admin"));
        await db.SaveChangesAsync();
    }
}
