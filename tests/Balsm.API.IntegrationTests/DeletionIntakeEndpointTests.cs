using System.Net;
using System.Net.Http.Headers;
using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// <c>POST /deletion/intake</c> over real HTTP. The apps send it with no body,
/// which model binding used to reject before the handler ran — a failure only
/// a request through the host can show.
/// </summary>
[Collection(nameof(LiteWebAppCollection))]
public sealed class DeletionIntakeEndpointTests(LiteWebAppFactory factory)
{
    [Fact]
    public async Task Intake_WithoutToken_IsRejected()
    {
        var response = await factory.CreateClient().PostAsync("/deletion/intake", content: null);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    public async Task Intake_WithNoBodyOrAnEmptyOne_IsNotAValidationError(string? json)
    {
        var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<JwtService>()
            .IssueAccessToken(Guid.NewGuid(), "nobody@example.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = json is null ? null : new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/deletion/intake", content);

        // The account does not exist, so this is not a 200 — but it must get
        // past binding: no 400 asking for a body or a field.
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest, body);
        body.Should().NotContain("request body is required").And.NotContain("field is required");
    }
}
