using System.Collections.Concurrent;
using System.Net;
using Balsm.Entity.Application.DTOs;
using Balsm.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Balsm.Supervisor.Tests.Services;

/// <summary>
/// Tests that read or write files next to the test assembly (AppContext.BaseDirectory),
/// or that mutate process-wide static state, run in this collection so nothing else
/// runs alongside them.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SupervisorBaseDirectoryCollection
{
    public const string Name = "Supervisor base directory (serial)";
}

/// <summary>A unique temp directory that is deleted on dispose.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"balsm-supervisor-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup of a temp directory.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup of a temp directory.
        }
    }
}

/// <summary>HTTP handler that answers from a delegate and records every request.</summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    : HttpMessageHandler
{
    public ConcurrentQueue<HttpRequestMessage> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Enqueue(request);
        return Task.FromResult(respond(request));
    }

    public static HttpResponseMessage Text(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body) };

    public static HttpResponseMessage Bytes(byte[] body)
        => new(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
}

internal sealed class StubHttpClientFactory(HttpMessageHandler handler, Uri? baseAddress = null)
    : IHttpClientFactory
{
    public List<string> RequestedNames { get; } = [];

    public HttpClient CreateClient(string name)
    {
        RequestedNames.Add(name);
        return new HttpClient(handler, disposeHandler: false) { BaseAddress = baseAddress };
    }
}

internal sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
{
    public int StopApplicationCalls { get; private set; }

    public CancellationToken ApplicationStarted => CancellationToken.None;

    public CancellationToken ApplicationStopping => CancellationToken.None;

    public CancellationToken ApplicationStopped => CancellationToken.None;

    public void StopApplication() => StopApplicationCalls++;
}

/// <summary>Logger that keeps every formatted entry so tests can assert on behaviour.</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    public ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Entries.Enqueue((logLevel, formatter(state, exception), exception));

    public bool Contains(LogLevel level, string fragment)
        => Entries.Any(e => e.Level == level && e.Message.Contains(fragment, StringComparison.Ordinal));
}

/// <summary>Configuration whose reads always fail, used to stop a code path at a known point.</summary>
internal sealed class ThrowingConfiguration : IConfiguration
{
    public string? this[string key]
    {
        get => throw new InvalidOperationException("configuration unavailable");
        set => throw new InvalidOperationException("configuration unavailable");
    }

    public IEnumerable<IConfigurationSection> GetChildren() => [];

    public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);

    public IConfigurationSection GetSection(string key)
        => throw new InvalidOperationException("configuration unavailable");
}

internal sealed class FakeServer : IServer
{
    public FakeServer(params string[] addresses)
    {
        var feature = new ServerAddressesFeature();
        foreach (var address in addresses)
        {
            feature.Addresses.Add(address);
        }

        Features.Set<IServerAddressesFeature>(feature);
    }

    public IFeatureCollection Features { get; } = new FeatureCollection();

    public void Dispose()
    {
    }

    public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        where TContext : notnull => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class ThrowingFeaturesServer : IServer
{
    public IFeatureCollection Features => throw new InvalidOperationException("features unavailable");

    public void Dispose()
    {
    }

    public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        where TContext : notnull => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Mediator that answers <c>GetWorkspaceQuery</c> with a fixed result.</summary>
internal sealed class FakeWorkspaceMediator(Result<WorkspaceDto> result) : IMediator
{
    public int SendCalls { get; private set; }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        SendCalls++;
        return Task.FromResult((TResponse)(object)result);
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest => throw new NotSupportedException();

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task Publish(object notification, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification => throw new NotSupportedException();
}

internal static class Poll
{
    /// <summary>Waits for a condition driven by another thread or process, bounded by a timeout.</summary>
    public static async Task<bool> UntilAsync(Func<bool> condition, int timeoutMs = 10_000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition())
        {
            if (Environment.TickCount64 > deadline)
            {
                return false;
            }

            await Task.Delay(20);
        }

        return true;
    }
}
