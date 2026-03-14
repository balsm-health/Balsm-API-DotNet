using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using Balsam.Supervisor.Configuration;
using Balsam.Supervisor.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsam.Supervisor.Services;

public sealed class SelfUpdateService
{
    private readonly SupervisorOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SelfUpdateService> _logger;

    private UpdateInfoResponse? _cachedUpdateInfo;

    public SelfUpdateService(
        IOptions<SupervisorOptions> options,
        IHostApplicationLifetime lifetime,
        IHttpClientFactory httpClientFactory,
        ILogger<SelfUpdateService> logger)
    {
        _options = options.Value;
        _lifetime = lifetime;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<UpdateInfoResponse> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("GitHub");
        var url = $"/repos/{_options.GitHubRepository}/releases/latest";

        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        var tagName = json.GetProperty("tag_name").GetString() ?? "";
        var latestVersion = tagName.TrimStart('v');
        var currentVersion = typeof(SelfUpdateService).Assembly
            .GetName().Version?.ToString() ?? "0.0.0";

        var rid = GetCurrentRid();
        var assetUrl = FindAssetUrl(json, rid);

        _cachedUpdateInfo = new UpdateInfoResponse
        {
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
            IsUpdateAvailable = IsNewer(latestVersion, currentVersion),
            ReleaseUrl = json.GetProperty("html_url").GetString(),
            DownloadUrl = assetUrl,
            ReleaseNotes = json.TryGetProperty("body", out var body) ? body.GetString() : null
        };

        return _cachedUpdateInfo;
    }

    public async Task ApplyUpdateAsync(CancellationToken ct = default)
    {
        if (_cachedUpdateInfo is not { IsUpdateAvailable: true, DownloadUrl: not null })
        {
            throw new InvalidOperationException(
                "No update available. Check for updates first.");
        }

        var client = _httpClientFactory.CreateClient("GitHub");
        var tempDir = Path.Combine(Path.GetTempPath(), $"balsam-update-{Guid.NewGuid():N}");
        var archivePath = Path.Combine(tempDir, "update.zip");

        try
        {
            Directory.CreateDirectory(tempDir);

            _logger.LogInformation("Downloading update from {Url}",
                _cachedUpdateInfo.DownloadUrl);

            await using var stream = await client.GetStreamAsync(
                _cachedUpdateInfo.DownloadUrl, ct);
            await using var file = File.Create(archivePath);
            await stream.CopyToAsync(file, ct);
            await file.FlushAsync(ct);
            file.Close();

            var stagingDir = Path.Combine(tempDir, "staged");
            _logger.LogInformation("Extracting update...");
            ZipFile.ExtractToDirectory(archivePath, stagingDir, overwriteFiles: true);

            var installDir = AppContext.BaseDirectory;
            _logger.LogInformation("Applying update to {Dir}", installDir);

            foreach (var sourceFile in Directory.EnumerateFiles(stagingDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(stagingDir, sourceFile);
                var destFile = Path.Combine(installDir, relativePath);
                var destDir = Path.GetDirectoryName(destFile);
                if (destDir is not null)
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(sourceFile, destFile, overwrite: true);
            }

            _logger.LogInformation("Update applied. Restarting...");
            _lifetime.StopApplication();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); }
                catch { /* Best effort cleanup */ }
            }
        }
    }

    internal static string GetCurrentRid()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "win-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? "osx-arm64"
                : "osx-x64";
        }
        return "linux-x64";
    }

    internal static string? FindAssetUrl(JsonElement release, string rid)
    {
        if (!release.TryGetProperty("assets", out var assets)) return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (name.Contains(rid, StringComparison.OrdinalIgnoreCase) &&
                (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                 name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)))
            {
                return asset.GetProperty("browser_download_url").GetString();
            }
        }
        return null;
    }

    internal static bool IsNewer(string latest, string current)
    {
        return Version.TryParse(latest, out var l)
            && Version.TryParse(current, out var c)
            && l > c;
    }
}
