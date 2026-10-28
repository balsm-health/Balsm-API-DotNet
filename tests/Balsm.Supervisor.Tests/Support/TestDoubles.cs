using System.Net;
using System.Text;
using System.Text.Json;
using Balsm.SharedKernel.Contracts;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Balsm.Supervisor.Tests.Support;

/// <summary>
/// Tests that touch process-wide state (files under AppContext.BaseDirectory,
/// Console streams, environment variables, static tokens) run in this collection,
/// which xUnit never runs in parallel with any other test.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessStateCollection
{
    public const string Name = "Supervisor process state";
}

internal sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
{
    public int StopRequests { get; private set; }

    public CancellationToken ApplicationStarted => CancellationToken.None;

    public CancellationToken ApplicationStopping => CancellationToken.None;

    public CancellationToken ApplicationStopped => CancellationToken.None;

    public void StopApplication() => StopRequests++;
}

internal sealed class FakeFirstRunOrchestrator : IFirstRunOrchestrator
{
    public List<(string Name, string Slug, string Locale)> Seeds { get; } = [];

    public Task SeedWorkspaceAsync(string name, string slug, string locale, CancellationToken ct = default)
    {
        Seeds.Add((name, slug, locale));
        return Task.CompletedTask;
    }
}

internal sealed class FakeServer : IServer
{
    public IFeatureCollection Features { get; } = new FeatureCollection();

    public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        where TContext : notnull => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose()
    {
    }
}

/// <summary>Answers every request through a delegate and records what was sent.</summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string?> Bodies { get; } = [];

    public static HttpResponseMessage Json(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
        return respond(request);
    }
}

internal sealed class StubHttpClientFactory(HttpMessageHandler handler, Uri? baseAddress = null)
    : IHttpClientFactory
{
    public List<string> CreatedNames { get; } = [];

    public HttpClient CreateClient(string name)
    {
        CreatedNames.Add(name);
        return new HttpClient(handler, disposeHandler: false) { BaseAddress = baseAddress };
    }
}

/// <summary>
/// Moves a file under AppContext.BaseDirectory aside for the duration of a test and
/// puts the original back (or removes the test's copy) on dispose.
/// </summary>
internal sealed class BaseDirectoryFileScope : IDisposable
{
    private readonly string? _backupPath;

    public BaseDirectoryFileScope(params string[] relativePath)
    {
        FilePath = Path.Combine([AppContext.BaseDirectory, .. relativePath]);
        if (File.Exists(FilePath))
        {
            _backupPath = FilePath + $".{Guid.NewGuid():N}.bak";
            File.Move(FilePath, _backupPath);
        }
    }

    public string FilePath { get; }

    public void Dispose()
    {
        if (File.Exists(FilePath))
        {
            File.SetAttributes(FilePath, FileAttributes.Normal);
            File.Delete(FilePath);
        }

        if (_backupPath is not null)
            File.Move(_backupPath, FilePath);
    }
}

internal static class ActionResultReader
{
    public static int? StatusOf(IActionResult result) => result switch
    {
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => null
    };

    public static JsonElement BodyOf(IActionResult result)
        => JsonSerializer.SerializeToElement(((ObjectResult)result).Value);
}
