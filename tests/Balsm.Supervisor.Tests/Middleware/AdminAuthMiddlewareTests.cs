using Balsm.Supervisor.Auth;
using Balsm.Supervisor.Middleware;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.Supervisor.Tests.Middleware;

public class AdminAuthMiddlewareTests
{
    private readonly AdminSessionService _sessionService = new();
    private readonly AdminAuthService _authService = SupervisorFactory.CreateAuthService(SupervisorFactory.CreateStore());
    private bool _nextCalled;

    [Theory]
    [InlineData("/api/v1/entities")]
    [InlineData("/api/v1/federation/heartbeat")]
    [InlineData("/admin/setup")]
    public async Task InvokeAsync_OutsideAdminApi_PassesThrough(string path)
    {
        var context = CreateContext(path);

        await CreateSut().InvokeAsync(context);

        _nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/v1/admin/auth/setup")]
    [InlineData("/api/v1/admin/auth/login")]
    [InlineData("/api/v1/admin/auth/status")]
    [InlineData("/api/v1/admin/auth/recovery/use")]
    [InlineData("/API/V1/ADMIN/AUTH/LOGIN")]
    public async Task InvokeAsync_PublicAuthEndpoint_PassesThroughWithoutSession(string path)
    {
        await _authService.SetupAsync("admin", "correct-horse-1");
        var context = CreateContext(path);

        await CreateSut().InvokeAsync(context);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenSetupNotDone_Returns403()
    {
        var context = CreateContext("/api/v1/admin/status");

        await CreateSut().InvokeAsync(context);

        _nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(403);
    }

    [Theory]
    [InlineData("/api/v1/admin/status")]
    [InlineData("/api/v1/admin/auth/me")]
    [InlineData("/api/v1/admin/auth/recovery/regenerate")]
    [InlineData("/api/v1/admin/auth/change-password")]
    [InlineData("/api/v1/admin/federation/pairings")]
    public async Task InvokeAsync_ProtectedEndpointWithoutCookie_Returns401(string path)
    {
        await _authService.SetupAsync("admin", "correct-horse-1");
        var context = CreateContext(path);

        await CreateSut().InvokeAsync(context);

        _nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(401);
    }

    [Theory]
    [InlineData("")]
    [InlineData("forged-session-token")]
    public async Task InvokeAsync_WithEmptyOrUnknownSessionCookie_Returns401(string token)
    {
        await _authService.SetupAsync("admin", "correct-horse-1");
        var context = CreateContext("/api/v1/admin/status");
        context.Request.Headers.Cookie = $"balsm_admin_session={token}";

        await CreateSut().InvokeAsync(context);

        _nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidatedSessionCookie_Returns401()
    {
        await _authService.SetupAsync("admin", "correct-horse-1");
        var token = _sessionService.CreateSession("admin");
        _sessionService.InvalidateSession(token);
        var context = CreateContext("/api/v1/admin/status");
        context.Request.Headers.Cookie = $"balsm_admin_session={token}";

        await CreateSut().InvokeAsync(context);

        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_WithValidSessionCookie_PassesThrough()
    {
        await _authService.SetupAsync("admin", "correct-horse-1");
        var token = _sessionService.CreateSession("admin");
        var context = CreateContext("/api/v1/admin/status");
        context.Request.Headers.Cookie = $"balsm_admin_session={token}";

        await CreateSut().InvokeAsync(context);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithLocalCliClaim_PassesThroughWithoutSession()
    {
        await _authService.SetupAsync("admin", "correct-horse-1");
        var context = CreateContext("/api/v1/admin/status");
        context.Items["LocalCli"] = true;

        await CreateSut().InvokeAsync(context);

        _nextCalled.Should().BeTrue();
    }

    private AdminAuthMiddleware CreateSut() => new(_ =>
    {
        _nextCalled = true;
        return Task.CompletedTask;
    });

    private DefaultHttpContext CreateContext(string path)
    {
        var services = new ServiceCollection()
            .AddSingleton(_sessionService)
            .AddSingleton(_authService)
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }
}
