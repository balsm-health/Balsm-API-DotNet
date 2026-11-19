using Balsm.Infrastructure.Maintenance;
using Balsm.Infrastructure.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Balsm.API.Tests.Maintenance;

public sealed class MaintenanceWriteGuardMiddlewareTests
{
    [Theory]
    [InlineData("POST", "/account/profile")]
    [InlineData("PUT", "/care-team/providers/1")]
    [InlineData("DELETE", "/sessions/abc")]
    [InlineData("POST", "/auth/password/sign-in")]
    public async Task InvokeAsync_InBlockMode_RefusesWriteWith503(string method, string path)
    {
        var (context, nextCalled) = await RunAsync("block", method, path);

        (context.Response.StatusCode, nextCalled, context.Response.Headers.RetryAfter.ToString())
            .Should().Be((503, false, "300"));
    }

    [Theory]
    [InlineData("GET", "/emergency-qr/scan/abc")]
    [InlineData("GET", "/api/v1/maintenance")]
    [InlineData("PUT", "/api/v1/admin/maintenance")]
    [InlineData("POST", "/auth/refresh")]
    [InlineData("POST", "/auth/sign-out")]
    [InlineData("POST", "/api/v1/federation/pair")]
    public async Task InvokeAsync_InBlockMode_LetsReadsAndEssentialWritesThrough(string method, string path)
    {
        var (_, nextCalled) = await RunAsync("block", method, path);

        nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("off")]
    [InlineData("readonly")]
    public async Task InvokeAsync_WhenNotBlocked_LetsWritesThrough(string mode)
    {
        var (_, nextCalled) = await RunAsync(mode, "POST", "/account/profile");

        nextCalled.Should().BeTrue();
    }

    private static async Task<(HttpContext Context, bool NextCalled)> RunAsync(string mode, string method, string path)
    {
        var nextCalled = false;
        var middleware = new MaintenanceWriteGuardMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new FixedMaintenance(new MaintenanceState(mode, null, null, null)));
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        return (context, nextCalled);
    }

    private sealed class FixedMaintenance(MaintenanceState state) : IMaintenanceModeService
    {
        public ValueTask<MaintenanceState> GetAsync(CancellationToken ct = default) => ValueTask.FromResult(state);

        public Task<Balsm.SharedKernel.Results.Result<MaintenanceState>> SetAsync(
            MaintenanceChange change, string actor, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
