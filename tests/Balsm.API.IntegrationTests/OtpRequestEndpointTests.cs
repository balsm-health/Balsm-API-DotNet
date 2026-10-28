using System.Net;
using System.Net.Http.Json;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// <c>POST /auth/otp/request</c> with purpose <c>signup</c> over real HTTP: the
/// 409 the app turns into "already registered", which only exists while
/// sign-up is strict.
///
/// The switch is read on every request, so it is flipped on the shared host's
/// configuration and put back after, rather than booting a second host.
/// </summary>
[Collection(nameof(LiteWebAppCollection))]
public sealed class OtpRequestEndpointTests(LiteWebAppFactory factory)
{
    [Fact]
    public async Task RequestOtp_SignUpForARegisteredEmail_WhenStrict_Returns409EmailRegistered()
    {
        await SeedIdentityAsync("taken@example.test");

        var response = await PostWithSwitchAsync("on", Body("taken@example.test", "signup"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("EmailRegistered");
    }

    [Fact]
    public async Task RequestOtp_SignUpForARegisteredEmail_WhenNotStrict_Returns200()
    {
        await SeedIdentityAsync("known@example.test");

        var response = await PostWithSwitchAsync("off", Body("known@example.test", "signup"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpResponseMessage> PostWithSwitchAsync(string revealExisting, object body)
    {
        var config = factory.Services.GetRequiredService<IConfiguration>();
        var before = config["Auth:SignUp:RevealExisting"];
        config["Auth:SignUp:RevealExisting"] = revealExisting;
        try
        {
            return await factory.CreateClient().PostAsJsonAsync("/auth/otp/request", body);
        }
        finally
        {
            config["Auth:SignUp:RevealExisting"] = before;
        }
    }

    // The API reads snake_case JSON, as the apps send it.
    private static object Body(string email, string purpose) =>
        new Dictionary<string, string> { ["email"] = email, ["country_code"] = "EG", ["purpose"] = purpose, ["language"] = "en" };

    private async Task SeedIdentityAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        db.UserIdentities.Add(UserIdentity.Create(Guid.NewGuid(), "email", email, email));
        await db.SaveChangesAsync();
    }
}
