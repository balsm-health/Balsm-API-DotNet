using Balsm.Supervisor.Services;
using FluentAssertions;
using Xunit;

namespace Balsm.Supervisor.Tests.Services;

public class SelfUpdateServiceTests
{
    [Theory]
    [InlineData("1.1.0", "1.0.0", true)]
    [InlineData("2.0.0", "1.9.9", true)]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.1.0", false)]
    [InlineData("0.9.0", "1.0.0", false)]
    public void IsNewer_ComparesVersionsCorrectly(string latest, string current, bool expected)
    {
        SelfUpdateService.IsNewer(latest, current).Should().Be(expected);
    }

    [Theory]
    [InlineData("invalid", "1.0.0", false)]
    [InlineData("1.0.0", "invalid", false)]
    [InlineData("", "", false)]
    public void IsNewer_HandlesInvalidVersions(string latest, string current, bool expected)
    {
        SelfUpdateService.IsNewer(latest, current).Should().Be(expected);
    }

    [Fact]
    public void GetCurrentRid_ReturnsValidRid()
    {
        var rid = SelfUpdateService.GetCurrentRid();

        rid.Should().BeOneOf("osx-arm64", "osx-x64", "linux-x64", "win-x64");
    }

    [Fact]
    public void FindChecksumUrl_FindsSha256SumsAsset()
    {
        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("""
        {
            "assets": [
                { "name": "balsm-osx-arm64.zip", "browser_download_url": "https://example.com/balsm.zip" },
                { "name": "SHA256SUMS", "browser_download_url": "https://example.com/SHA256SUMS" }
            ]
        }
        """);

        var url = SelfUpdateService.FindChecksumUrl(json);
        url.Should().Be("https://example.com/SHA256SUMS");
    }

    [Fact]
    public void FindChecksumUrl_ReturnsNullWhenNotFound()
    {
        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("""
        {
            "assets": [
                { "name": "balsm-osx-arm64.zip", "browser_download_url": "https://example.com/balsm.zip" }
            ]
        }
        """);

        var url = SelfUpdateService.FindChecksumUrl(json);
        url.Should().BeNull();
    }

    [Fact]
    public void ParseChecksumForFile_ExtractsCorrectHash()
    {
        var content = """
        abc123def456  balsm-osx-arm64.zip
        789abc012def  balsm-linux-x64.zip
        """;

        var hash = SelfUpdateService.ParseChecksumForFile(content, "balsm-osx-arm64.zip");
        hash.Should().Be("abc123def456");
    }

    [Fact]
    public void ParseChecksumForFile_ReturnsNullForMissingFile()
    {
        var content = """
        abc123def456  balsm-osx-arm64.zip
        """;

        var hash = SelfUpdateService.ParseChecksumForFile(content, "balsm-win-x64.zip");
        hash.Should().BeNull();
    }

    [Fact]
    public void FindAssetInfo_ReturnsUrlAndFileName()
    {
        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("""
        {
            "assets": [
                { "name": "balsm-osx-arm64.zip", "browser_download_url": "https://example.com/balsm-osx-arm64.zip" },
                { "name": "balsm-linux-x64.zip", "browser_download_url": "https://example.com/balsm-linux-x64.zip" }
            ]
        }
        """);

        var (url, fileName) = SelfUpdateService.FindAssetInfo(json, "osx-arm64");
        url.Should().Be("https://example.com/balsm-osx-arm64.zip");
        fileName.Should().Be("balsm-osx-arm64.zip");
    }
}
