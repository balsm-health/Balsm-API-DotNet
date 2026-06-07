using System.Reflection;
using System.Text.Json;

namespace Balsm.Supervisor.Cli;

/// <summary>
/// balsm version — prints the assembly version without starting the server.
/// </summary>
internal static class VersionCommand
{
    public static Task<int> RunAsync(bool json)
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";

        if (json)
        {
            var envelope = JsonSerializer.Serialize(new
            {
                command = "version",
                status = "ok",
                result = new { version }
            }, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(envelope);
        }
        else
        {
            Console.WriteLine($"balsm {version}");
        }

        return Task.FromResult(0);
    }
}
