using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Balsm.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.API.IntegrationTests;

[Collection(nameof(LiteWebAppCollection))]
public sealed class GetSelfEndpointTests(LiteWebAppFactory factory)
{
    [Fact]
    public async Task GetSelf_WhenAccountDoesNotExist_Returns404ProblemDetails()
    {
        var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<JwtService>()
            .IssueAccessToken(Guid.NewGuid(), "nobody@example.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/account/self");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Status.Should().Be(404);
    }
}