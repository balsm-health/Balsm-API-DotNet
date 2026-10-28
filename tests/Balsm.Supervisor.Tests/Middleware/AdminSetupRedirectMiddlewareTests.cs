using Balsm.Supervisor.Auth;
using Balsm.Supervisor.Middleware;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.Supervisor.Tests.Middleware;

public class AdminSetupRedirectMiddlewareTests
{
    private readonly AdminAuthService _authService = SupervisorFactory.CreateAuthService(SupervisorFactory.CreateStore());
    private bool _nextCalled;

    [Fact]
    public async Task InvokeAsync_PostSetupAfterSetupDone_Returns403()
    {
        await _authService.SetupAsync("admin", "correct-horse-1");
        var context = CreateContext("POST", "/api/v1/admin/auth/setup");

        await CreateSut().InvokeAsync(context);

        _nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(403);
        ReadBody(context).Should().Contain("Setup already completed");
    }

    [Fact]
    public async Task InvokeAsync_PostSetupBeforeSetupDone_PassesThrough()
    {
        var context = CreateContext("POST", "/API/V1/ADMIN/AUTH/SETUP");

        await CreateSut().InvokeAsync(context);

        _nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("GET", "/api/v1/admin/auth/setup")]
    [InlineData("POST", "/api/v1/admin/auth/login")]
    public async Task InvokeAsync_OtherRequests_PassThroughEvenAfterSetup(string method, string path)
    {
        await _authService.SetupAsync("admin", "correct-horse-1");
        var context = CreateContext(method, path);

        await CreateSut().InvokeAsync(context);

        _nextCalled.Should().BeTrue();
    }

    private AdminSetupRedirectMiddleware CreateSut() => new(_ =>
    {
        _nextCalled = true;
        return Task.CompletedTask;
    });

    private DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton(_authService).BuildServiceProvider()
        };
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body).ReadToEnd();
    }
}
