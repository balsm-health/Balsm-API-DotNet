using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsm.Supervisor.Services;

public sealed class SelfUpdateService
{
    private readonly SupervisorOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SelfUpdateService> _logger;

    private UpdateInfoResponse? _cachedUpdateInfo;
    private JsonElement? _cachedRelease;

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
        _cachedRelease = json;

        var tagName = json.GetProperty("tag_name").GetString() ?? "";
        var latestVersion = tagName.TrimStart('v');
        var currentVersion = typeof(SelfUpdateService).Assembly
            .GetName().Version?.ToString() ?? "0.0.0";

        var rid = GetCurrentRid();
        var (assetUrl, assetFileName) = FindAssetInfo(json, rid);
        var checksumUrl = FindChecksumUrl(json);

        _cachedUpdateInfo = new UpdateInfoResponse
        {
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
            IsUpdateAvailable = IsNewer(latestVersion, currentVersion),
            ReleaseUrl = json.GetProperty("html_url").GetString(),
            DownloadUrl = assetUrl,
            AssetFileName = assetFileName,
            ChecksumUrl = checksumUrl,
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
        var tempDir = Path.Combine(Path.GetTempPath(), $"balsm-update-{Guid.NewGuid():N}");
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

            // SHA256 hash verification
            if (_cachedUpdateInfo.ChecksumUrl is not null
                && _cachedUpdateInfo.AssetFileName is not null)
            {
                _logger.LogInformation("Verifying SHA256 hash...");
                var checksumContent = await client.GetStringAsync(
                    _cachedUpdateInfo.ChecksumUrl, ct);
                var expectedHash = ParseChecksumForFile(
                    checksumContent, _cachedUpdateInfo.AssetFileName);

                if (expectedHash is not null)
                {
                    await using var fileStream = File.OpenRead(archivePath);
                    var hashBytes = await SHA256.HashDataAsync(fileStream, ct);
                    var actualHash = Convert.ToHexStringLower(hashBytes);

                    if (!string.Equals(actualHash, expectedHash,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"SHA256 mismatch. Expected: {expectedHash}, " +
                            $"Actual: {actualHash}. Update aborted.");
                    }

                    _logger.LogInformation("SHA256 hash verified successfully");
                }
            }

            var stagingDir = Path.Combine(tempDir, "staged");
            _logger.LogInformation("Extracting update...");
            ZipFile.ExtractToDirectory(archivePath, stagingDir, overwriteFiles: true);

            var installDir = AppContext.BaseDirectory;
            _logger.LogInformation("Applying update to {Dir}", installDir);

            foreach (var sourceFile in Directory.EnumerateFiles(
                stagingDir, "*", SearchOption.AllDirectories))
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

    internal static (string? Url, string? FileName) FindAssetInfo(
        JsonElement release, string rid)
    {
        if (!release.TryGetProperty("assets", out var assets))
            return (null, null);

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (name.Contains(rid, StringComparison.OrdinalIgnoreCase) &&
                (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                 name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)))
            {
                var url = asset.GetProperty("browser_download_url").GetString();
                return (url, name);
            }
        }
        return (null, null);
    }

    internal static string? FindAssetUrl(JsonElement release, string rid)
    {
        var (url, _) = FindAssetInfo(release, rid);
        return url;
    }

    internal static string? FindChecksumUrl(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets)) return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (name.Contains("sha256", StringComparison.OrdinalIgnoreCase)
                || name.Contains("checksums", StringComparison.OrdinalIgnoreCase))
            {
                return asset.GetProperty("browser_download_url").GetString();
            }
        }
        return null;
    }

    internal static string? ParseChecksumForFile(string checksumContent, string fileName)
    {
        foreach (var line in checksumContent.Split('\n',
            StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split([' ', '\t'],
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2
                && parts[1].Contains(fileName, StringComparison.OrdinalIgnoreCase))
            {
                return parts[0];
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
