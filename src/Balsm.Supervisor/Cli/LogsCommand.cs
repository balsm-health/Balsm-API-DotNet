using System.Text.Json;

namespace Balsm.Supervisor.Cli;

/// <summary>
/// balsm logs — view the local Serilog file logs over loopback using the CLI token.
///   balsm logs                 tail the latest log file (200 lines)
///   balsm logs --lines 500     tail N lines
///   balsm logs --list          list available log files
///   balsm logs --file NAME     tail a specific file
///   balsm logs --json          machine-readable output
/// Exits 0 on success, 4 when the server is unreachable.
/// </summary>
internal static class LogsCommand
{
    private const int DefaultHttpPort = 5050;

    public static async Task<int> RunAsync(string[] args, bool json)
    {
        var token = ReadToken();
        if (token is null)
        {
            Console.Error.WriteLine("Local CLI token not found. Is the server running?");
            return 4;
        }

        var list = args.Contains("--list");
        var lines = ReadIntFlag(args, "--lines") ?? 200;
        var file = ReadStringFlag(args, "--file");

        var port = ReadPort();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.Add("X-Balsm-Local-Token", token);
        var baseUrl = $"http://127.0.0.1:{port}/api/v1/admin/logs";

        try
        {
            if (list)
            {
                var body = await client.GetStringAsync($"{baseUrl}/files");
                if (json) { Console.WriteLine(body); return 0; }

                using var doc = JsonDocument.Parse(body);
                var dir = doc.RootElement.TryGetProperty("directory", out var d) ? d.GetString() : "?";
                Console.WriteLine($"  directory : {dir}");
                foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
                {
                    var name = item.GetProperty("name").GetString();
                    var size = item.GetProperty("sizeBytes").GetInt64();
                    var mod = item.GetProperty("lastModified").GetDateTime();
                    Console.WriteLine($"  {mod:yyyy-MM-dd HH:mm}  {FormatBytes(size),10}  {name}");
                }
                return 0;
            }

            var url = $"{baseUrl}/tail?lines={lines}";
            if (!string.IsNullOrWhiteSpace(file)) url += $"&file={Uri.EscapeDataString(file)}";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"Server responded {(int)response.StatusCode}");
                return 5;
            }

            var text = await response.Content.ReadAsStringAsync();
            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { command = "logs", status = "ok", lines = text.Split('\n') },
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine(text);
            }
            return 0;
        }
        catch (HttpRequestException)
        {
            Console.Error.WriteLine("Cannot reach the server. Is it running?");
            return 4;
        }
        catch (TaskCanceledException)
        {
            Console.Error.WriteLine("Server did not respond in time.");
            return 4;
        }
    }

    private static string? ReadToken()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "var", "local-cli.token");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    private static int ReadPort()
    {
        var envPort = Environment.GetEnvironmentVariable("BALSM_HTTP_PORT");
        return envPort is not null && int.TryParse(envPort, out var p) ? p : DefaultHttpPort;
    }

    private static int? ReadIntFlag(string[] args, string flag)
    {
        var i = Array.IndexOf(args, flag);
        return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var v) ? v : null;
    }

    private static string? ReadStringFlag(string[] args, string flag)
    {
        var i = Array.IndexOf(args, flag);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.#} {units[unit]}";
    }
}
