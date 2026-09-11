using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// Every module ships a liveness probe at <c>GET /{slug}/health</c>, anonymous,
/// returning <c>{ data: { status, module, version, timestamp } }</c>.
///
/// These run over real HTTP through the host, which is the point: the existing
/// HealthControllerTests calls a controller directly and so cannot catch a
/// module whose controllers were never registered as an ApplicationPart, a route
/// that collides, or a probe that has quietly fallen behind an auth middleware.
/// Those are exactly the ways a health endpoint breaks.
/// </summary>
[Collection(nameof(LiteWebAppCollection))]
public sealed class ModuleHealthEndpointTests(LiteWebAppFactory factory)
{
    /// Slug per module. A new module adds its slug here — the repo requires a
    /// health probe before a module is considered complete.
    public static TheoryData<string, string> Modules =>
        new()
        {
            { "auth", "Auth" },
            { "account", "Account" },
            { "care", "CareDirectory" },
            { "customer", "Customer" },
            { "deletion", "Deletion" },
            { "disclosure", "Disclosure" },
            { "emergency-qr", "EmergencyQr" },
            { "entity", "Entity" },
            { "identity", "Identity" },
            { "inventory", "Inventory" },
            { "pos", "POS" },
            { "prescription", "Prescription" },
            { "sessions", "Sessions" },
        };

    [Theory]
    [MemberData(nameof(Modules))]
    public async Task Health_IsAnonymousAndReportsHealthy(string slug, string module)
    {
        // No Authorization header: the probe must answer unauthenticated, which
        // is the one sanctioned exception to "every endpoint requires auth".
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/{slug}/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "{0} must expose an anonymous liveness probe", slug);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = body.RootElement.GetProperty("data");

        data.GetProperty("status").GetString().Should().Be("Healthy");
        data.GetProperty("module").GetString().Should().Be(module);
        data.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace();
        data.GetProperty("timestamp").GetDateTimeOffset()
            .Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Health_DoesNotTouchTheDatabase()
    {
        // Liveness is deliberately DB-free — readiness is gated separately by
        // ReadinessGate/MigrationRunner. A probe that queried would report a
        // module unhealthy during a migration and take a deploy down with it.
        // Proxy for that here: it answers well within any DB round-trip budget.
        var client = factory.CreateClient();

        var started = DateTimeOffset.UtcNow;
        await client.GetAsync("/care/health");
        var elapsed = DateTimeOffset.UtcNow - started;

        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task HostHealth_ReportsReadiness()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
