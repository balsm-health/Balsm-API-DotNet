using System.Text;
using Balsm.Infrastructure.Diagnostics;
using Balsm.Infrastructure.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Balsm.API.Tests.Diagnostics;

/// <summary>
/// Verifies the <c>Debug:RawValues</c> un-redaction switch is honored ONLY in
/// the Development environment — the hard gate that keeps plaintext PHI /
/// credentials out of non-dev logs.
/// </summary>
public sealed class DebugRequestLoggingMiddlewareTests
{
    private sealed class CapturingLogger : ILogger<DebugRequestLoggingMiddleware>
    {
        public readonly List<string> Lines = [];
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex,
            Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, ex));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class FakeEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static async Task<string> RunAsync(bool rawValues, string environment)
    {
        var logger = new CapturingLogger();
        var options = Options.Create(new DebugLoggingOptions
        {
            LogRequests = true,
            LogBodies = true,
            RawValues = rawValues,
        });
        var mw = new DebugRequestLoggingMiddleware(
            next: ctx => Task.CompletedTask,
            logger: logger,
            options: options,
            environment: new FakeEnv(environment));

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Path = "/auth/otp/verify";
        ctx.Request.ContentType = "application/json";
        var payload = Encoding.UTF8.GetBytes("""{"code":"123456","email":"a@b.com"}""");
        ctx.Request.Body = new MemoryStream(payload);
        ctx.Request.ContentLength = payload.Length;
        ctx.Response.Body = new MemoryStream();

        await mw.InvokeAsync(ctx);
        return string.Join("\n", logger.Lines);
    }

    /// Runs a response larger than the body cap through the middleware.
    private static async Task<string> RunOversizedResponseAsync(bool raw = false, int cap = 256)
    {
        var logger = new CapturingLogger();
        var options = Options.Create(new DebugLoggingOptions
        {
            LogRequests = true,
            LogBodies = true,
            RawValues = raw,
            MaxBodyBytes = cap,
        });

        // Valid JSON, comfortably over the cap, so the captured slice ends
        // mid-token — exactly what a care-directory response does at 8 KiB.
        var big = "{\"data\":[" + string.Join(",", Enumerable.Range(0, 200).Select(i => $"{{\"id\":\"place-{i}\"}}")) + "]}";

        var mw = new DebugRequestLoggingMiddleware(
            next: async ctx =>
            {
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(big);
            },
            logger: logger,
            options: options,
            environment: new FakeEnv(Environments.Development));

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/care/entities";
        ctx.Response.Body = new MemoryStream();

        await mw.InvokeAsync(ctx);
        return string.Join("\n", logger.Lines);
    }

    [Fact]
    public async Task Oversized_response_is_reported_not_parsed()
    {
        // Parsing a body cut at the cap always throws, and a debugger set to
        // break on JsonException halts the server mid-request — the client then
        // sees a timeout with no server-side error to explain it.
        var log = await RunOversizedResponseAsync();

        log.Should().Contain("truncated");
        log.Should().Contain("not parsed");
        log.Should().NotContain("non-json body redacted",
            because: "that placeholder means the parse was attempted and failed");
    }

    [Fact]
    public async Task Oversized_response_in_raw_mode_still_shows_the_prefix()
    {
        var log = await RunOversizedResponseAsync(raw: true);

        log.Should().Contain("place-0", because: "raw mode logs what was captured");
        log.Should().Contain("truncated");
    }

    [Fact]
    public async Task Response_within_the_cap_is_still_scrubbed()
    {
        // The fix must not disable scrubbing for normal-sized bodies.
        var logger = new CapturingLogger();
        var options = Options.Create(new DebugLoggingOptions
        {
            LogRequests = true,
            LogBodies = true,
            RawValues = false,
        });
        var mw = new DebugRequestLoggingMiddleware(
            next: async ctx =>
            {
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync("""{"email":"a@b.com"}""");
            },
            logger: logger,
            options: options,
            environment: new FakeEnv(Environments.Development));

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/account/profile";
        ctx.Response.Body = new MemoryStream();

        await mw.InvokeAsync(ctx);

        var log = string.Join("\n", logger.Lines);
        log.Should().Contain(SensitiveDataScrubber.Redacted);
        log.Should().NotContain("a@b.com");
    }

    [Fact]
    public async Task RawValues_in_Development_logs_the_real_body()
    {
        var log = await RunAsync(rawValues: true, environment: Environments.Development);

        log.Should().Contain("123456"); // the OTP code prints un-redacted
        log.Should().NotContain(SensitiveDataScrubber.Redacted);
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task RawValues_outside_Development_is_ignored_body_stays_redacted(string env)
    {
        var log = await RunAsync(rawValues: true, environment: env);

        log.Should().NotContain("123456"); // hard gate: never un-redacts off-dev
        log.Should().Contain(SensitiveDataScrubber.Redacted);
    }

    [Fact]
    public async Task Default_redacts_even_in_Development()
    {
        var log = await RunAsync(rawValues: false, environment: Environments.Development);

        log.Should().NotContain("123456");
        log.Should().Contain(SensitiveDataScrubber.Redacted);
    }
}
