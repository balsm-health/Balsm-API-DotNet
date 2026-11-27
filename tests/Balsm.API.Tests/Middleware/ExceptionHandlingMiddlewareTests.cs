using System.Text.Json;
using Balsm.Infrastructure.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.API.Tests.Middleware;

public sealed class ExceptionHandlingMiddlewareTests
{
    public static TheoryData<Exception, int> Mappings => new()
    {
        { new ArgumentException("bad"), 400 },
        { new UnauthorizedAccessException(), 401 },
        { new KeyNotFoundException(), 404 },
        { new InvalidOperationException(), 409 },
        { new NotSupportedException(), 500 },
    };

    [Theory]
    [MemberData(nameof(Mappings))]
    public async Task InvokeAsync_Throws_MapsTheExceptionToAStatus(Exception exception, int expected)
    {
        var context = await RunAsync(exception);

        context.Response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task InvokeAsync_Throws_WritesProblemDetailsWithTheCorrelationId()
    {
        var context = await RunAsync(new KeyNotFoundException(), correlationId: "corr-9");

        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        (context.Response.ContentType, doc.RootElement.GetProperty("correlationId").GetString())
            .Should().Be(("application/problem+json", "corr-9"));
    }

    [Fact]
    public async Task InvokeAsync_NoException_LeavesTheResponseAlone()
    {
        var context = new DefaultHttpContext();
        var middleware = new ExceptionHandlingMiddleware(
            _ => Task.CompletedTask, NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(200);
    }

    private static async Task<HttpContext> RunAsync(Exception exception, string? correlationId = null)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        if (correlationId is not null) context.Items["CorrelationId"] = correlationId;
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exception, NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        return context;
    }
}
