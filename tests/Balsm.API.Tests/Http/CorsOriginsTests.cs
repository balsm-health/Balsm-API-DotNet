using Balsm.API.Http;
using FluentAssertions;
using Xunit;

namespace Balsm.API.Tests.Http;

/// <summary>
/// The patient web app is served from another host than the API, so its origin
/// must be allowed by name — and nothing that merely resembles it.
/// </summary>
public sealed class CorsOriginsTests
{
    private static readonly IReadOnlySet<string> Configured =
        CorsOrigins.Parse("https://app.balsm.health, https://staging.example.test/");

    [Theory]
    [InlineData("https://app.balsm.health")]
    [InlineData("https://APP.balsm.health")]
    [InlineData("https://staging.example.test")]
    [InlineData("http://localhost:5173")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("https://balsm-clinic.local")]
    public void Allows_listed_and_local_origins(string origin) =>
        CorsOrigins.IsAllowed(origin, Configured).Should().BeTrue();

    [Theory]
    [InlineData("http://app.balsm.health")]
    [InlineData("https://app.balsm.health:8443")]
    [InlineData("https://evil.app.balsm.health")]
    [InlineData("https://app.balsm.health.evil.test")]
    [InlineData("https://balsm.health")]
    [InlineData("null")]
    [InlineData("")]
    public void Rejects_everything_else(string origin) =>
        CorsOrigins.IsAllowed(origin, Configured).Should().BeFalse();

    [Fact]
    public void Nothing_configured_leaves_only_local_origins()
    {
        var none = CorsOrigins.Parse(null);

        none.Should().BeEmpty();
        CorsOrigins.IsAllowed("https://app.balsm.health", none).Should().BeFalse();
        CorsOrigins.IsAllowed("http://localhost:3000", none).Should().BeTrue();
    }

    [Theory]
    [InlineData("*")]
    [InlineData("app.balsm.health")]
    [InlineData("ftp://app.balsm.health")]
    [InlineData("https://app.balsm.health/some/path")]
    public void Drops_entries_that_are_not_an_origin(string entry) =>
        CorsOrigins.Parse(entry).Should().BeEmpty();
}
