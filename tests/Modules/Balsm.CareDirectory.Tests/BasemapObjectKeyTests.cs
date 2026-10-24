using Xunit;
using Balsm.CareDirectory.Infrastructure.MapPacks;

namespace Balsm.CareDirectory.Tests;

/// <summary>Parsing "packs/{id}-{version}.pmtiles" keys back into their parts.</summary>
public sealed class BasemapObjectKeyTests
{
    [Fact]
    public void ASimpleIdParsesCleanly()
    {
        var result = BasemapObjectKey.Parse("packs/cairo-20260913.pmtiles");

        Assert.NotNull(result);
        Assert.Equal("cairo", result!.Value.GovernorateId);
        Assert.Equal("20260913", result.Value.Version);
    }

    [Theory]
    [InlineData("packs/beni-suef-20260913.pmtiles", "beni-suef")]
    [InlineData("packs/kafr-el-sheikh-20260913.pmtiles", "kafr-el-sheikh")]
    [InlineData("packs/red-sea-20260913.pmtiles", "red-sea")]
    public void AHyphenatedIdIsNotSplitAtTheWrongHyphen(string key, string expectedId)
    {
        // The id's own hyphens must not be mistaken for the id/version separator.
        var result = BasemapObjectKey.Parse(key);

        Assert.NotNull(result);
        Assert.Equal(expectedId, result!.Value.GovernorateId);
        Assert.Equal("20260913", result.Value.Version);
    }

    [Theory]
    [InlineData("places/cairo-20260913.ndjson.gz")]      // wrong prefix
    [InlineData("packs/cairo-2026091.pmtiles")]           // 7-digit version
    [InlineData("packs/cairo-20260913.pmtiles.bak")]      // wrong extension
    [InlineData("packs/CAIRO-20260913.pmtiles")]          // uppercase id
    [InlineData("packs/manifest.json")]                   // unrelated object
    public void ANonMatchingKeyParsesToNull(string key)
    {
        Assert.Null(BasemapObjectKey.Parse(key));
    }
}
