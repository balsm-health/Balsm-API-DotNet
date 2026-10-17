using System.Net;
using Balsm.CareDirectory.Api;
using Balsm.CareDirectory.Api.Controllers;
using Balsm.Infrastructure.RateLimit;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.CareDirectory.Tests;

/// <summary>
/// The public care directory is anonymous, so a per-IP limit is the only
/// brake on it.
/// </summary>
public sealed class CareDirectoryRateLimitTests : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ServiceProvider _services;
    private readonly CareDirectoryRateLimitAttribute _filter = new();

    public CareDirectoryRateLimitTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<IRateLimitStore>(new InMemoryRateLimitStore(_cache))
            .BuildServiceProvider();
    }

    [Fact]
    public void Controller_CarriesTheLimit()
    {
        typeof(CareController).IsDefined(typeof(CareDirectoryRateLimitAttribute), inherit: false)
            .Should().BeTrue("every care-directory route is anonymous and must be rate limited");
    }

    [Fact]
    public async Task UnderLimit_RunsTheAction()
    {
        var (context, ran) = await InvokeAsync("203.0.113.7");

        ran.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task OverLimit_Returns429WithRetryAfter_AndSkipsTheAction()
    {
        for (var i = 0; i < CareDirectoryRateLimitAttribute.Limit; i++)
            await InvokeAsync("203.0.113.7");

        var (context, ran) = await InvokeAsync("203.0.113.7");

        ran.Should().BeFalse();
        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        int.Parse(context.HttpContext.Response.Headers.RetryAfter.ToString()).Should().BeInRange(1, 60);
    }

    [Fact]
    public async Task OneAddressOverLimit_DoesNotBlockAnother()
    {
        for (var i = 0; i <= CareDirectoryRateLimitAttribute.Limit; i++)
            await InvokeAsync("203.0.113.7");

        var (context, ran) = await InvokeAsync("198.51.100.4");

        ran.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    private async Task<(ResourceExecutingContext Context, bool Ran)> InvokeAsync(string ip)
    {
        var http = new DefaultHttpContext { RequestServices = _services };
        http.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var context = new ResourceExecutingContext(actionContext, [], []);

        var ran = false;
        await _filter.OnResourceExecutionAsync(context, () =>
        {
            ran = true;
            return Task.FromResult(new ResourceExecutedContext(actionContext, []));
        });
        return (context, ran);
    }

    public void Dispose()
    {
        _services.Dispose();
        _cache.Dispose();
    }
}
