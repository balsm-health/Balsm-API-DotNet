using System.Diagnostics;
using System.Text;
using Balsm.Infrastructure.Diagnostics;
using Microsoft.AspNetCore.Http;
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
    IOptions<DebugLoggingOptions> options)
{
    private readonly DebugLoggingOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        var req = context.Request;
        var correlationId = context.Items.TryGetValue("CorrelationId", out var cid)
            ? cid?.ToString() ?? "-"
            : "-";
        var query = SensitiveDataScrubber.ScrubQuery(req.QueryString.Value ?? string.Empty);

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
        var scrubbed = SensitiveDataScrubber.ScrubJson(text);
        return req.ContentLength > cap ? scrubbed + " …[truncated]" : scrubbed;
    }

    private string? ReadCaptured(MemoryStream buffer, string? contentType)
    {
        if (!IsJson(contentType) || buffer.Length == 0) return null;

        var cap = _options.MaxBodyBytes;
        var len = (int)Math.Min(buffer.Length, cap);
        var bytes = new byte[len];
        _ = buffer.Read(bytes, 0, len);
        var scrubbed = SensitiveDataScrubber.ScrubJson(Encoding.UTF8.GetString(bytes));
        return buffer.Length > cap ? scrubbed + " …[truncated]" : scrubbed;
    }

    private static string ScrubHeaders(IHeaderDictionary headers)
    {
        var sb = new StringBuilder("{");
        var first = true;
        foreach (var (key, value) in headers)
        {
            if (!first) sb.Append(", ");
            first = false;
            sb.Append(key).Append('=');
            sb.Append(SensitiveDataScrubber.SensitiveHeaders.Contains(key)
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
