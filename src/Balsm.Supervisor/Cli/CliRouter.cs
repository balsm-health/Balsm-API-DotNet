namespace Balsm.Supervisor.Cli;

/// <summary>
/// Entry point for CLI subcommands. Called from Program.cs before the web host starts.
/// Returns the exit code, or -1 if args do not match a CLI command (start the server instead).
/// </summary>
public static class CliRouter
{
    private static readonly HashSet<string> Commands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "status", "version", "backup", "db", "admin", "audit", "mode"
        };

    public static bool IsCliInvocation(string[] args)
        => args.Length > 0 && Commands.Contains(args[0]);

    public static async Task<int> RunAsync(string[] args)
    {
        var json = args.Contains("--json");

        return args[0].ToLowerInvariant() switch
        {
            "status"  => await StatusCommand.RunAsync(json),
            "version" => await VersionCommand.RunAsync(json),
            _         => UsageError(args[0])
        };
    }

    private static int UsageError(string cmd)
    {
        Console.Error.WriteLine($"balsm: unknown command '{cmd}'. Try: status, version");
        return 2;
    }
}
