using System.Diagnostics;
using System.Text;
using Balsm.Infrastructure.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsm.Infrastructure.Middleware;

/// <summary>
/// Debug-only HTTP tracer: logs method, path, scrubbed query, status, elapsed,
/// and (when <c>Debug:LogBodies</c> is on) scrubbed request/response bodies.
/// Registered in <c>Program.cs</c> only when <c>Debug:LogRequests</c> is set and
/// the environment is NOT Production. All output is redacted by
/// <see cref="SensitiveDataScrubber"/> so no PHI/credential reaches the log.
/// </summary>
public sealed class DebugRequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<DebugRequestLoggingMiddleware> logger,
    IOptions<DebugLoggingOptions> options,
    IHostEnvironment environment)
{
    private readonly DebugLoggingOptions _options = options.Value;

    /// <summary>
    /// True only when the developer opted into raw (un-redacted) output AND the
    /// host is Development. The environment half is a hard gate: a stray
    /// <c>Debug:RawValues=true</c> in a Staging/Production config is ignored, so
    /// plaintext PHI/credentials can never reach a non-dev log.
    /// </summary>
    private readonly bool _raw = options.Value.RawValues && environment.IsDevelopment();

    private static bool _warnedRaw;

    public async Task InvokeAsync(HttpContext context)
    {
        if (_raw && !_warnedRaw)
        {
            _warnedRaw = true;
            logger.LogWarning(
                "Debug:RawValues is ON — request bodies, headers, and query strings "
                + "are being logged UN-REDACTED (Development only). PHI and credentials "
                + "are in plaintext. Never enable this outside local development.");
        }

        var req = context.Request;
        var correlationId = context.Items.TryGetValue("CorrelationId", out var cid)
            ? cid?.ToString() ?? "-"
            : "-";
        var query = _raw
            ? req.QueryString.Value ?? string.Empty
            : SensitiveDataScrubber.ScrubQuery(req.QueryString.Value ?? string.Empty);

        var requestBody = _options.LogBodies ? await CaptureRequestBodyAsync(req) : null;

        // Only buffer the response for non-static paths (API + SPA fallback). A
        // request with a file extension is a static asset — copying it through a
        // MemoryStream would waste memory and could break SendFileAsync.
        var captureResponse = _options.LogBodies && !HasFileExtension(req.Path);

        var start = Stopwatch.GetTimestamp();

        if (!captureResponse)
        {
            try
            {
                await next(context);
            }
            finally
            {
                Log(context, correlationId, query, requestBody, responseBody: null, start);
            }
            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context);
            buffer.Position = 0;
            var responseBody = ReadCaptured(buffer, context.Response.ContentType);
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody);
            Log(context, correlationId, query, requestBody, responseBody, start);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private void Log(HttpContext context, string correlationId, string query,
        string? requestBody, string? responseBody, long start)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var req = context.Request;
        var headers = ScrubHeaders(req.Headers);

        logger.LogDebug(
            "HTTP {Method} {Path}{Query} → {StatusCode} in {ElapsedMs:0.0} ms " +
            "[cid={CorrelationId}] headers={Headers}{RequestBody}{ResponseBody}",
            req.Method, req.Path.Value, query, context.Response.StatusCode, elapsedMs,
            correlationId, headers,
            requestBody is null ? string.Empty : $" req={requestBody}",
            responseBody is null ? string.Empty : $" resp={responseBody}");
    }

    private async Task<string?> CaptureRequestBodyAsync(HttpRequest req)
    {
        if (!IsJson(req.ContentType) || req.ContentLength is null or 0) return null;

        req.EnableBuffering();
        var cap = _options.MaxBodyBytes;
        var chunk = new byte[Math.Min(cap, (int)Math.Min(req.ContentLength.Value, int.MaxValue))];
        var read = await ReadFullyAsync(req.Body, chunk);
        req.Body.Position = 0;

        var text = Encoding.UTF8.GetString(chunk, 0, read);
        var truncated = req.ContentLength > cap;
        return Describe(text, truncated);
    }

    private string? ReadCaptured(MemoryStream buffer, string? contentType)
    {
        if (!IsJson(contentType) || buffer.Length == 0) return null;

        var cap = _options.MaxBodyBytes;
        var len = (int)Math.Min(buffer.Length, cap);
        var bytes = new byte[len];
        _ = buffer.Read(bytes, 0, len);
        var text = Encoding.UTF8.GetString(bytes);
        return Describe(text, truncated: buffer.Length > cap);
    }

    /// <summary>
    /// Renders a captured body for the log, scrubbed unless raw mode is on.
    /// </summary>
    /// <remarks>
    /// A body cut at <c>MaxBodyBytes</c> ends mid-token, so parsing it as JSON
    /// always throws. That exception was caught and the placeholder returned, so
    /// nothing broke — but it fired on every response over 8 KiB, which any
    /// care-directory query is, and a debugger set to break on JsonException
    /// halts the server mid-request. The client then sees a timeout with no
    /// server-side error to explain it.
    ///
    /// So do not parse what is known to be incomplete: report the size instead.
    /// The scrubber's own catch stays as the guard for bodies that are whole but
    /// not JSON.
    /// </remarks>
    private string Describe(string text, bool truncated)
    {
        if (truncated)
        {
            return _raw
                ? text + " …[truncated]"
                : $"[truncated at {_options.MaxBodyBytes} bytes, not parsed]";
        }

        return _raw ? text : SensitiveDataScrubber.ScrubJson(text);
    }

    private string ScrubHeaders(IHeaderDictionary headers)
    {
        var sb = new StringBuilder("{");
        var first = true;
        foreach (var (key, value) in headers)
        {
            if (!first) sb.Append(", ");
            first = false;
            sb.Append(key).Append('=');
            sb.Append(!_raw && SensitiveDataScrubber.SensitiveHeaders.Contains(key)
                ? SensitiveDataScrubber.Redacted
                : value.ToString());
        }
        return sb.Append('}').ToString();
    }

    private static bool IsJson(string? contentType) =>
        contentType is not null && contentType.Contains("json", StringComparison.OrdinalIgnoreCase);

    private static bool HasFileExtension(PathString path) =>
        Path.HasExtension(path.Value);

    private static async Task<int> ReadFullyAsync(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total));
            if (read == 0) break;
            total += read;
        }
        return total;
    }
}
