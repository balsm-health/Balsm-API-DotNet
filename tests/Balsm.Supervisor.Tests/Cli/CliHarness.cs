using System.Net;
using System.Net.Sockets;
using Balsm.Supervisor.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Balsm.Supervisor.Tests.Cli;

/// <summary>
/// Sets up what the CLI commands read from the process: the loopback token file under
/// AppContext.BaseDirectory, the BALSM_HTTP_PORT variable and the Console streams.
/// Everything is put back on dispose. Use only from <see cref="ProcessStateCollection"/>.
/// </summary>
internal sealed class CliHarness : IAsyncDisposable
{
    public const string Token = "0123456789abcdef0123456789abcdef";

    private readonly BaseDirectoryFileScope _tokenFile = new("var", "local-cli.token");
    private readonly string? _originalPort = Environment.GetEnvironmentVariable("BALSM_HTTP_PORT");
    private readonly TextWriter _originalOut = Console.Out;
    private readonly TextWriter _originalError = Console.Error;
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private WebApplication? _server;

    public CliHarness()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
    }

    public string Out => _out.ToString();

    public string Error => _error.ToString();

    public List<string> RequestedPathsAndQueries { get; } = [];

    public void WriteToken()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_tokenFile.FilePath)!);
        File.WriteAllText(_tokenFile.FilePath, $"{Token}\n");
    }

    /// <summary>Points the CLI at a loopback port with nothing listening on it.</summary>
    public void UseClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        Environment.SetEnvironmentVariable("BALSM_HTTP_PORT", port.ToString());
    }

    /// <summary>Starts an in-process loopback stub of the admin API and points the CLI at it.</summary>
    public async Task StartServerAsync(Func<HttpContext, Task> handle)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _server = builder.Build();
        _server.Run(async context =>
        {
            RequestedPathsAndQueries.Add($"{context.Request.Path}{context.Request.QueryString}");
            if (context.Request.Headers["X-Balsm-Local-Token"] != Token)
            {
                context.Response.StatusCode = 401;
                return;
            }

            await handle(context);
        });
        await _server.StartAsync();
        var port = new Uri(_server.Urls.Single()).Port;
        Environment.SetEnvironmentVariable("BALSM_HTTP_PORT", port.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        Environment.SetEnvironmentVariable("BALSM_HTTP_PORT", _originalPort);
        _tokenFile.Dispose();
        var varDir = Path.GetDirectoryName(_tokenFile.FilePath)!;
        if (Directory.Exists(varDir) && !Directory.EnumerateFileSystemEntries(varDir).Any())
            Directory.Delete(varDir);
        if (_server is not null)
            await _server.DisposeAsync();
    }
}
