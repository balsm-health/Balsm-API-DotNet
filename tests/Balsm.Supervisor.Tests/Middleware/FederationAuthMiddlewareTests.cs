using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Middleware;
using Balsm.Supervisor.Models;
using Balsm.Supervisor.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Balsm.Supervisor.Tests.Middleware;

public class FederationAuthMiddlewareTests
{
    private readonly MiddlewareTestFederationStore _store = new();
    private readonly FederationService _federationService;

    public FederationAuthMiddlewareTests()
    {
        var options = Options.Create(new SupervisorOptions { ServerId = "local-server" });
        _federationService = new FederationService(
            _store, options, NullLogger<FederationService>.Instance);
    }

    [Fact]
    public async Task NonFederationPath_PassesThrough()
    {
        var nextCalled = false;
        var middleware = new FederationAuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateContext("/api/v1/admin/status");

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task AdminFederationPath_PassesThrough()
    {
        var nextCalled = false;
        var middleware = new FederationAuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateContext("/api/v1/admin/federation/pairings");

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task PairEndpoint_PassesThroughWithoutApiKey()
    {
        var nextCalled = false;
        var middleware = new FederationAuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateContext("/api/v1/federation/pair");

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task HeartbeatEndpoint_WithoutApiKey_Returns401()
    {
        var middleware = new FederationAuthMiddleware(_ => Task.CompletedTask);
        var context = CreateContext("/api/v1/federation/heartbeat");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task HeartbeatEndpoint_WithInvalidApiKey_Returns403()
    {
        var middleware = new FederationAuthMiddleware(_ => Task.CompletedTask);
        var invalidKey = Convert.ToBase64String(new byte[32]);
        var context = CreateContext("/api/v1/federation/heartbeat", invalidKey);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task HeartbeatEndpoint_WithValidApiKey_PassesThrough()
    {
        // Create a pairing to generate a valid API key
        var codeResponse = _federationService.GenerateCode();
        var pairResult = await _federationService.ValidateAndCompletePairingAsync(
            new PairRequest
            {
                Code = codeResponse.Code,
                ServerId = "remote",
                ServerName = "Remote",
                ServerUrl = "https://remote.example.com",
                ApiKey = Convert.ToBase64String(new byte[32])
            });

        var validApiKey = pairResult!.ApiKey;

        var nextCalled = false;
        var middleware = new FederationAuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateContext("/api/v1/federation/heartbeat", validApiKey);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Items["FederationPairing"].Should().NotBeNull();
        context.Items["FederationPairing"].Should().BeOfType<ServerPairing>();
    }

    private HttpContext CreateContext(string path, string? apiKey = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_federationService);
        var serviceProvider = services.BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };
        context.Request.Path = path;

        if (apiKey is not null)
        {
            context.Request.Headers["X-Balsm-ApiKey"] = apiKey;
        }

        return context;
    }
}

// Re-use InMemoryFederationStore from FederationServiceTests if in the same assembly,
// or define it here for standalone compilation.
internal sealed class MiddlewareTestFederationStore : IFederationStore
{
    private FederationData _data = new();

    public Task<FederationData> LoadAsync(CancellationToken ct = default)
        => Task.FromResult(_data);

    public Task SaveAsync(FederationData data, CancellationToken ct = default)
    {
        _data = data;
        return Task.CompletedTask;
    }
}
