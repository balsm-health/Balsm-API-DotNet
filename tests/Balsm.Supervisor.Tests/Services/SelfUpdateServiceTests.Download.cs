using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Balsm.Supervisor.Tests.Services;

public partial class SelfUpdateServiceTests
{
    private const string ReleaseUrl = "https://github.example.test/releases/v99.0.0";
    private const string DownloadBase = "https://downloads.example.test/";
    private const string GitHubApiBase = "https://api.github.example.test";

    private readonly FakeHostApplicationLifetime _lifetime = new();

    [Fact]
    public void FindAssetInfo_WhenReleaseHasNoAssets_ReturnsNulls()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("""{ "tag_name": "v1.0.0" }""");

        var (url, fileName) = SelfUpdateService.FindAssetInfo(json, "linux-x64");

        url.Should().BeNull();
        fileName.Should().BeNull();
    }

    [Fact]
    public void FindAssetInfo_WhenOnlyNonArchiveMatchesRid_ReturnsNulls()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("""
        { "assets": [ { "name": "balsm-linux-x64.exe", "browser_download_url": "https://example.test/a" } ] }
        """);

        var (url, fileName) = SelfUpdateService.FindAssetInfo(json, "linux-x64");

        url.Should().BeNull();
        fileName.Should().BeNull();
    }

    [Fact]
    public void FindAssetUrl_WithTarGzAsset_ReturnsDownloadUrl()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("""
        { "assets": [ { "name": "balsm-linux-x64.tar.gz", "browser_download_url": "https://example.test/balsm-linux-x64.tar.gz" } ] }
        """);

        var url = SelfUpdateService.FindAssetUrl(json, "linux-x64");

        url.Should().Be("https://example.test/balsm-linux-x64.tar.gz");
    }

    [Fact]
    public void FindChecksumUrl_WithChecksumsTxtAsset_ReturnsDownloadUrl()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("""
        { "assets": [ { "name": "checksums.txt", "browser_download_url": "https://example.test/checksums.txt" } ] }
        """);

        var url = SelfUpdateService.FindChecksumUrl(json);

        url.Should().Be("https://example.test/checksums.txt");
    }

    [Fact]
    public void FindChecksumUrl_WhenReleaseHasNoAssets_ReturnsNull()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("""{ "tag_name": "v1.0.0" }""");

        var url = SelfUpdateService.FindChecksumUrl(json);

        url.Should().BeNull();
    }

    [Fact]
    public void ParseChecksumForFile_WithSingleColumnLines_SkipsThem()
    {
        var content = "orphan-hash-without-name\nabc123\tbalsm-linux-x64.zip\n";

        var hash = SelfUpdateService.ParseChecksumForFile(content, "balsm-linux-x64.zip");

        hash.Should().Be("abc123");
    }

    [Fact]
    public async Task CheckForUpdateAsync_WithNewerRelease_ReturnsUpdateInfo()
    {
        var assetName = AssetName();
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Text(ReleaseJson("v99.0.0", assetName)));
        var factory = new StubHttpClientFactory(handler, new Uri(GitHubApiBase));
        var sut = CreateSut(factory);

        var info = await sut.CheckForUpdateAsync();

        info.Should().BeEquivalentTo(new
        {
            LatestVersion = "99.0.0",
            IsUpdateAvailable = true,
            ReleaseUrl,
            DownloadUrl = DownloadBase + assetName,
            AssetFileName = assetName,
            ChecksumUrl = DownloadBase + "SHA256SUMS",
            ReleaseNotes = "Synthetic release notes.",
        });
        handler.Requests.Single().RequestUri!.AbsolutePath
            .Should().Be("/repos/example-org/example-repo/releases/latest");
    }

    [Fact]
    public async Task CheckForUpdateAsync_WithOlderReleaseWithoutAssetsOrBody_ReportsNoUpdate()
    {
        var json = $$"""{ "tag_name": "v0.0.1", "html_url": "{{ReleaseUrl}}" }""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Text(json));
        var sut = CreateSut(new StubHttpClientFactory(handler, new Uri(GitHubApiBase)));

        var info = await sut.CheckForUpdateAsync();

        info.Should().BeEquivalentTo(new
        {
            IsUpdateAvailable = false,
            DownloadUrl = (string?)null,
            ChecksumUrl = (string?)null,
            ReleaseNotes = (string?)null,
        });
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenGitHubReturnsError_ThrowsHttpRequestException()
    {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Text("unavailable", HttpStatusCode.InternalServerError));
        var sut = CreateSut(new StubHttpClientFactory(handler, new Uri(GitHubApiBase)));

        var act = () => sut.CheckForUpdateAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ApplyUpdateAsync_WithoutPriorCheck_ThrowsInvalidOperation()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Text(""));
        var sut = CreateSut(new StubHttpClientFactory(handler));

        var act = () => sut.ApplyUpdateAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Check for updates first*");
    }

    [Fact]
    public async Task ApplyUpdateAsync_WhenChecksumMismatches_AbortsWithoutInstallingOrRestarting()
    {
        var installedName = $"selfupdate-test-{Guid.NewGuid():N}.txt";
        var zip = BuildZip(installedName, "synthetic payload");
        var sut = await CreateCheckedSutAsync(zip, checksums: $"{new string('0', 64)}  {AssetName()}\n");

        var act = () => sut.ApplyUpdateAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("SHA256 mismatch*");
        File.Exists(Path.Combine(AppContext.BaseDirectory, installedName)).Should().BeFalse();
        _lifetime.StopApplicationCalls.Should().Be(0);
    }

    [Fact]
    public async Task ApplyUpdateAsync_WhenChecksumMatches_InstallsFilesAndStopsHost()
    {
        var installedDir = $"selfupdate-test-{Guid.NewGuid():N}";
        var zip = BuildZip($"{installedDir}/payload.txt", "synthetic payload");
        var hash = Convert.ToHexStringLower(SHA256.HashData(zip));
        var sut = await CreateCheckedSutAsync(zip, checksums: $"{hash}  {AssetName()}\n");
        var installedPath = Path.Combine(AppContext.BaseDirectory, installedDir, "payload.txt");

        try
        {
            await sut.ApplyUpdateAsync();

            (await File.ReadAllTextAsync(installedPath)).Should().Be("synthetic payload");
            _lifetime.StopApplicationCalls.Should().Be(1);
        }
        finally
        {
            Directory.Delete(Path.Combine(AppContext.BaseDirectory, installedDir), recursive: true);
        }
    }

    [Fact]
    public async Task ApplyUpdateAsync_WhenChecksumFileLacksAsset_InstallsWithoutVerification()
    {
        var installedName = $"selfupdate-test-{Guid.NewGuid():N}.txt";
        var zip = BuildZip(installedName, "unverified payload");
        var sut = await CreateCheckedSutAsync(zip, checksums: "abc123  some-other-file.zip\n");
        var installedPath = Path.Combine(AppContext.BaseDirectory, installedName);

        try
        {
            await sut.ApplyUpdateAsync();

            File.Exists(installedPath).Should().BeTrue();
            _lifetime.StopApplicationCalls.Should().Be(1);
        }
        finally
        {
            File.Delete(installedPath);
        }
    }

    [Fact]
    public async Task ApplyUpdateAsync_WhenReleaseHasNoChecksumAsset_InstallsWithoutVerification()
    {
        var installedName = $"selfupdate-test-{Guid.NewGuid():N}.txt";
        var zip = BuildZip(installedName, "unverified payload");
        var sut = await CreateCheckedSutAsync(zip, checksums: null);
        var installedPath = Path.Combine(AppContext.BaseDirectory, installedName);

        try
        {
            await sut.ApplyUpdateAsync();

            File.Exists(installedPath).Should().BeTrue();
        }
        finally
        {
            File.Delete(installedPath);
        }
    }

    private SelfUpdateService CreateSut(StubHttpClientFactory factory)
        => new(
            Options.Create(new SupervisorOptions { GitHubRepository = "example-org/example-repo" }),
            _lifetime,
            factory,
            NullLogger<SelfUpdateService>.Instance);

    private async Task<SelfUpdateService> CreateCheckedSutAsync(byte[] zip, string? checksums)
    {
        var assetName = AssetName();
        var release = ReleaseJson("v99.0.0", assetName, includeChecksums: checksums is not null);
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsoluteUri switch
        {
            var u when u.EndsWith("/releases/latest", StringComparison.Ordinal) => StubHttpMessageHandler.Text(release),
            var u when u == DownloadBase + assetName => StubHttpMessageHandler.Bytes(zip),
            var u when u == DownloadBase + "SHA256SUMS" => StubHttpMessageHandler.Text(checksums!),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        var sut = CreateSut(new StubHttpClientFactory(handler, new Uri(GitHubApiBase)));
        await sut.CheckForUpdateAsync();
        return sut;
    }

    private static string AssetName() => $"balsm-{SelfUpdateService.GetCurrentRid()}.zip";

    private static string ReleaseJson(string tag, string assetName, bool includeChecksums = true)
    {
        var checksumAsset = includeChecksums
            ? $$""", { "name": "SHA256SUMS", "browser_download_url": "{{DownloadBase}}SHA256SUMS" }"""
            : "";
        return $$"""
        {
            "tag_name": "{{tag}}",
            "html_url": "{{ReleaseUrl}}",
            "body": "Synthetic release notes.",
            "assets": [
                { "name": "{{assetName}}", "browser_download_url": "{{DownloadBase}}{{assetName}}" }{{checksumAsset}}
            ]
        }
        """;
    }

    private static byte[] BuildZip(string entryName, string content)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(content);
        }

        return buffer.ToArray();
    }
}
