using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Balsm.Infrastructure.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// <c>POST /auth/otp/verify-link</c> with a link token that matches no challenge
/// (issue #22). An expired or replayed magic link is an expected outcome, not a
/// fault: it must answer 401 ProblemDetails from the controller itself instead of
/// escaping to <see cref="ExceptionHandlingMiddleware"/>, which logs it at Error
/// and reports it to Sentry.
/// </summary>
[Collection(nameof(LiteWebAppCollection))]
public sealed class VerifyOtpLinkEndpointTests(LiteWebAppFactory factory)
{
    private static readonly object InvalidLinkBody = new
    {
        token = "not-a-real-link-token",
        device_id = "00000000-0000-0000-0000-000000000001",
        device_label = "test-device",
    };

    [Fact]
    public async Task VerifyOtpLink_WhenLinkIsInvalid_Returns401ProblemDetails()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/otp/verify-link", InvalidLinkBody);

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, body);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("status").GetInt32().Should().Be(401);
        json.RootElement.GetProperty("title").GetString().Should().Be("Unauthorized");
        json.RootElement.GetProperty("type").GetString().Should().Be("https://httpstatuses.io/401");
        json.RootElement.TryGetProperty("correlationId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task VerifyOtpLink_WhenLinkIsInvalid_DoesNotReachExceptionMiddleware()
    {
        var middlewareLogger = new CapturingLogger<ExceptionHandlingMiddleware>();
        using var app = factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
            services.AddSingleton<ILogger<ExceptionHandlingMiddleware>>(middlewareLogger)));
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/otp/verify-link", InvalidLinkBody);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        middlewareLogger.Errors.Should().BeEmpty(
            "an invalid link is an expected 401, not an unhandled exception for Sentry");
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly List<string> _errors = [];

        public IReadOnlyList<string> Errors
        {
            get { lock (_errors) return [.. _errors]; }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel < LogLevel.Error) return;
            lock (_errors) _errors.Add($"{formatter(state, exception)} {exception?.GetType().Name}");
        }
    }
}
