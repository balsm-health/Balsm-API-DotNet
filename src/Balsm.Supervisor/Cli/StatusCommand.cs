using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Balsm.Supervisor.Cli;

/// <summary>
/// balsm status — calls GET /api/v1/admin/status on loopback using the local CLI token.
/// Exits 0 when server is healthy, 4 when server is not reachable.
/// </summary>
internal static class StatusCommand
{
    private const int DefaultHttpPort = 5050;

    public static async Task<int> RunAsync(bool json)
    {
        var token = ReadToken();
        if (token is null)
        {
            Console.Error.WriteLine("Local CLI token not found. Is the server running?");
            return 4;
        }

        var port = ReadPort();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.Add("X-Balsm-Local-Token", token);

        try
        {
            var url = $"http://127.0.0.1:{port}/api/v1/admin/status";
            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"Server responded {(int)response.StatusCode}");
                return 5;
            }

            var body = await response.Content.ReadAsStringAsync();

            if (json)
            {
                var envelope = JsonSerializer.Serialize(new
                {
                    command = "status",
                    status = "ok",
                    result = JsonSerializer.Deserialize<JsonElement>(body)
                }, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(envelope);
                return 0;
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var running = root.TryGetProperty("isRunning", out var r) && r.GetBoolean();
            var mode    = root.TryGetProperty("mode",      out var m) ? m.GetString() : "?";
            var version = root.TryGetProperty("version",   out var v) ? v.GetString() : "?";
            var pid     = root.TryGetProperty("pid",       out var p) ? p.GetInt32().ToString() : "?";
            var httpPort = root.TryGetProperty("httpPort", out var hp) ? hp.GetInt32() : port;

            Console.WriteLine($"  status   : {(running ? "running" : "stopped")}");
            Console.WriteLine($"  version  : {version}");
            Console.WriteLine($"  mode     : {mode}");
            Console.WriteLine($"  pid      : {pid}");
            Console.WriteLine($"  admin UI : http://127.0.0.1:{httpPort}/admin");
            return 0;
        }
        catch (HttpRequestException)
        {
            Console.Error.WriteLine("Cannot reach the server. Is it running?");
            return 4;
        }
        catch (TaskCanceledException)
        {
            Console.Error.WriteLine("Server did not respond within 5 seconds.");
            return 4;
        }
    }

    private static string? ReadToken()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "var", "local-cli.token");
        if (!File.Exists(path)) return null;
        return File.ReadAllText(path).Trim();
    }

    private static int ReadPort()
    {
        var envPort = Environment.GetEnvironmentVariable("BALSM_HTTP_PORT");
        if (envPort is not null && int.TryParse(envPort, out var p)) return p;
        return DefaultHttpPort;
    }
}
