using System.Net;
using System.Text;
using Balsm.Supervisor.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Balsm.Supervisor.Tests.Middleware;

public sealed class RateLimitMiddlewareTests : IDisposable
{
    private static readonly IPAddress ClientIp = IPAddress.Parse("192.0.2.20");

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private int _nextStatus = StatusCodes.Status401Unauthorized;
    private int _nextCalls;

    [Fact]
    public async Task InvokeAsync_AfterFiveFailedAttempts_Returns429WithoutCallingNext()
    {
        var sut = CreateSut();
        for (var i = 0; i < 5; i++)
            await sut.InvokeAsync(LoginContext("admin@example.test"));
        var context = LoginContext("admin@example.test");

        await sut.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(429);
        _nextCalls.Should().Be(5);
    }

    [Fact]
    public async Task InvokeAsync_LockedOutResponses_CountAsFailures()
    {
        _nextStatus = StatusCodes.Status423Locked;
        var sut = CreateSut();
        for (var i = 0; i < 5; i++)
            await sut.InvokeAsync(LoginContext("admin@example.test"));
        var context = LoginContext("admin@example.test");

        await sut.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task InvokeAsync_UsernameCaseVariants_ShareTheSameBudget()
    {
        var sut = CreateSut();
        for (var i = 0; i < 5; i++)
            await sut.InvokeAsync(LoginContext(i % 2 == 0 ? "Admin@Example.test" : "admin@example.test"));
        var context = LoginContext("ADMIN@EXAMPLE.TEST");

        await sut.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task InvokeAsync_SuccessfulLogin_ResetsFailureCount()
    {
        var sut = CreateSut();
        for (var i = 0; i < 4; i++)
            await sut.InvokeAsync(LoginContext("admin@example.test"));
        _nextStatus = StatusCodes.Status200OK;
        await sut.InvokeAsync(LoginContext("admin@example.test"));
        _nextStatus = StatusCodes.Status401Unauthorized;
        for (var i = 0; i < 4; i++)
            await sut.InvokeAsync(LoginContext("admin@example.test"));
        var context = LoginContext("admin@example.test");

        await sut.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_OtherStatusCodes_DoNotChangeFailureCount()
    {
        _nextStatus = StatusCodes.Status400BadRequest;
        var sut = CreateSut();
        for (var i = 0; i < 6; i++)
            await sut.InvokeAsync(LoginContext("admin@example.test"));
        var context = LoginContext("admin@example.test");

        await sut.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task InvokeAsync_JsonBody_IsRewoundForDownstreamReaders()
    {
        string? downstreamBody = null;
        var sut = new RateLimitMiddleware(async ctx =>
        {
            downstreamBody = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
        }, _cache);

        await sut.InvokeAsync(LoginContext("admin@example.test"));

        downstreamBody.Should().Contain("admin@example.test");
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{\"password\":\"x\"}")]
    public async Task InvokeAsync_BodyWithoutReadableUsername_StillReachesNext(string body)
    {
        string? downstreamBody = null;
        var sut = new RateLimitMiddleware(async ctx =>
        {
            downstreamBody = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
        }, _cache);

        await sut.InvokeAsync(Context(body, "application/json", ClientIp));

        downstreamBody.Should().Be(body);
    }

    [Fact]
    public async Task InvokeAsync_NonJsonRequestsWithoutAddress_AreLimitedTogether()
    {
        var sut = CreateSut();
        for (var i = 0; i < 5; i++)
            await sut.InvokeAsync(Context("username=a", "application/x-www-form-urlencoded", null));
        var context = Context("username=b", "application/x-www-form-urlencoded", null);

        await sut.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(429);
    }

    public void Dispose() => _cache.Dispose();

    private RateLimitMiddleware CreateSut() => new(ctx =>
    {
        _nextCalls++;
        ctx.Response.StatusCode = _nextStatus;
        return Task.CompletedTask;
    }, _cache);

    private static DefaultHttpContext LoginContext(string username)
        => Context($"{{\"username\":\"{username}\",\"password\":\"irrelevant\"}}", "application/json; charset=utf-8", ClientIp);

    private static DefaultHttpContext Context(string body, string contentType, IPAddress? remote)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remote;
        context.Request.Method = "POST";
        context.Request.ContentType = contentType;
        context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
        context.Response.Body = new MemoryStream();
        return context;
    }
}
