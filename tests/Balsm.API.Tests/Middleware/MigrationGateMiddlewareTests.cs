using Balsm.Infrastructure.Lifecycle;
using Balsm.Infrastructure.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Balsm.API.Tests.Middleware;

public sealed class MigrationGateMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_GateReady_PassesThrough()
    {
        var (context, nextCalled) = await RunAsync(new ReadinessGate(), "/account/me");

        (nextCalled, context.Response.StatusCode).Should().Be((true, 200));
    }

    [Fact]
    public async Task InvokeAsync_GateClosed_Returns503WithTheReason()
    {
        var gate = new ReadinessGate();
        gate.SetNotReady("migration");

        var (context, nextCalled) = await RunAsync(gate, "/account/me");

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        (nextCalled, context.Response.StatusCode, body)
            .Should().Be((false, 503, """{"ready":false,"reason":"migration"}"""));
    }

    [Theory]
    [InlineData("/api/v1/health")]
    [InlineData("/API/V1/SERVER-INFO")]
    [InlineData("/openapi/v1/all.json")]
    [InlineData("/scalar/v1")]
    public async Task InvokeAsync_GateClosedOnAMetadataPath_PassesThrough(string path)
    {
        var gate = new ReadinessGate();
        gate.SetNotReady("migration");

        var (_, nextCalled) = await RunAsync(gate, path);

        nextCalled.Should().BeTrue();
    }

    private static async Task<(HttpContext Context, bool NextCalled)> RunAsync(ReadinessGate gate, string path)
    {
        var nextCalled = false;
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        var middleware = new MigrationGateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, gate);

        await middleware.InvokeAsync(context);

        return (context, nextCalled);
    }
}
