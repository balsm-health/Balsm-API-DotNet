using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.API.Controllers;

/// <summary>
/// Exposes the rolling Serilog file logs (var/logs/balsm-*.log) to the super-admin
/// portal and the `balsm logs` CLI. Read-only: list, tail, and download.
/// </summary>
[ApiController]
[Route("api/v1/admin/logs")]
public class LogsController : ControllerBase
{
    // Keep in sync with the File sink path resolved in Program.cs.
    private static string ResolveLogDirectory(IConfiguration config) =>
        Path.GetFullPath(config["Logs:Directory"] ?? "var/logs", AppContext.BaseDirectory);

    private readonly string _logDir;

    public LogsController(IConfiguration config)
    {
        _logDir = ResolveLogDirectory(config);
    }

    /// <summary>GET /api/v1/admin/logs/files — list available log files, newest first.</summary>
    [HttpGet("files")]
    public IActionResult ListFiles()
    {
        if (!Directory.Exists(_logDir))
            return Ok(new { directory = _logDir, items = Array.Empty<object>() });

        var items = new DirectoryInfo(_logDir)
            .EnumerateFiles("*.log")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new
            {
                name = f.Name,
                sizeBytes = f.Length,
                lastModified = f.LastWriteTimeUtc
            })
            .ToList();

        return Ok(new { directory = _logDir, items });
    }

    /// <summary>GET /api/v1/admin/logs/tail?file=&amp;lines= — last N lines of a log file as plain text.</summary>
    [HttpGet("tail")]
    public IActionResult Tail([FromQuery] string? file = null, [FromQuery] int lines = 200)
    {
        if (lines is < 1 or > 5000) lines = 200;

        var path = ResolveFileOrLatest(file);
        if (path is null)
            return NotFound(new { error = "Log file not found." });

        var tail = ReadLastLines(path, lines);
        return Content(string.Join('\n', tail), "text/plain", Encoding.UTF8);
    }

    /// <summary>GET /api/v1/admin/logs/download?file= — download a log file verbatim.</summary>
    [HttpGet("download")]
    public IActionResult Download([FromQuery] string file)
    {
        var path = ResolveSafePath(file);
        if (path is null || !System.IO.File.Exists(path))
            return NotFound(new { error = "Log file not found." });

        // Shared read so the download succeeds while Serilog holds the file open.
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return File(stream, "text/plain", Path.GetFileName(path));
    }

    /// <summary>
    /// Maps a caller-supplied file name to an absolute path inside the log
    /// directory, rejecting anything that escapes it (path traversal guard).
    /// </summary>
    private string? ResolveSafePath(string? file)
    {
        if (string.IsNullOrWhiteSpace(file)) return null;

        // Reject directory separators / traversal outright — log files are flat.
        var name = Path.GetFileName(file);
        if (name != file) return null;

        var full = Path.GetFullPath(Path.Combine(_logDir, name));
        var dirWithSep = _logDir.EndsWith(Path.DirectorySeparatorChar)
            ? _logDir
            : _logDir + Path.DirectorySeparatorChar;
        if (!full.StartsWith(dirWithSep, StringComparison.Ordinal)) return null;
        if (Path.GetExtension(full) != ".log") return null;

        return full;
    }

    /// <summary>Resolve the requested file, or fall back to the most recent log file.</summary>
    private string? ResolveFileOrLatest(string? file)
    {
        if (!string.IsNullOrWhiteSpace(file))
        {
            var path = ResolveSafePath(file);
            return path is not null && System.IO.File.Exists(path) ? path : null;
        }

        if (!Directory.Exists(_logDir)) return null;
        return new DirectoryInfo(_logDir)
            .EnumerateFiles("*.log")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }

    /// <summary>Read the last <paramref name="count"/> lines without loading the whole file.</summary>
    private static List<string> ReadLastLines(string path, int count)
    {
        var buffer = new LinkedList<string>();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            buffer.AddLast(line);
            if (buffer.Count > count) buffer.RemoveFirst();
        }

        return [.. buffer];
    }
}
