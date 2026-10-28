using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// <c>DeletionController</c> request-then-cancel flow over real HTTP, for an
/// account created by the API's own OTP sign-in.
/// </summary>
[Collection(nameof(LiteWebAppCollection))]
public sealed class DeletionEndpointTests(LiteWebAppFactory factory)
{
    [Fact]
    public async Task Intake_SignedIn_ReturnsAGraceWindowAWeekOut()
    {
        var client = await SignedInClientAsync("deletion-intake@example.test");

        var response = await client.PostAsJsonAsync("/deletion/intake", new Dictionary<string, string>
        {
            ["reason_code"] = "user_request",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TestAuth.ReadDataAsync(response)).GetProperty("grace_until").GetDateTime()
            .Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Cancel_PendingDeletion_ReturnsCancelled()
    {
        var client = await SignedInClientAsync("deletion-cancel@example.test");
        await client.PostAsync("/deletion/intake", content: null);

        var response = await client.PostAsync("/deletion/cancel", content: null);

        (await TestAuth.ReadDataAsync(response)).GetProperty("cancelled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_NothingPending_Returns409CancellationFailed()
    {
        var client = await SignedInClientAsync("deletion-cancel-none@example.test");

        var response = await client.PostAsync("/deletion/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await TestAuth.ReadErrorCodeAsync(response)).Should().Be("CancellationFailed");
    }

    [Fact]
    public async Task Cancel_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().PostAsync("/deletion/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> SignedInClientAsync(string email)
    {
        var user = await TestAuth.SignInAsync(factory, email);
        return TestAuth.CreateClient(factory, user.AccessToken);
    }
}
