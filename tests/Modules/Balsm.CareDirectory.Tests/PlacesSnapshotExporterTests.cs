using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Xunit;
using Balsm.CareDirectory.Domain.Entities;
using Balsm.CareDirectory.Infrastructure.MapPacks;

namespace Balsm.CareDirectory.Tests;

/// <summary>The gzipped ndjson snapshot uploaded for one governorate's places.</summary>
public sealed class PlacesSnapshotExporterTests
{
    private static CarePlace Place(string nameEn, double lat, double lng) =>
        CarePlace.Create(
            type: "pharmacy", nameEn: nameEn, nameAr: "صيدلية",
            addressEn: "1 Test St", addressAr: null,
            lat: lat, lng: lng, countryCode: "EG", source: "overture");

    [Fact]
    public void EachPlaceBecomesOneNdjsonLineWithSnakeCaseFields()
    {
        var places = new[] { Place("Al Ezaby", 30.05, 31.24), Place("Seif", 30.06, 31.25) };

        var snapshot = PlacesSnapshotExporter.Build(places);

        var lines = Decompress(snapshot.GzipBytes);
        Assert.Equal(2, lines.Length);
        Assert.Equal(2, snapshot.Count);

        using var doc = JsonDocument.Parse(lines[0]);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("name_en", out _));
        Assert.True(root.TryGetProperty("address_en", out _));
        Assert.True(root.TryGetProperty("lat", out _));
        Assert.True(root.TryGetProperty("lng", out _));
        // distance_km is caller-relative and meaningless for an offline file.
        Assert.False(root.TryGetProperty("distance_km", out _));
    }

    [Fact]
    public void AnEmptyGovernorateProducesAValidZeroCountSnapshot()
    {
        var snapshot = PlacesSnapshotExporter.Build([]);

        Assert.Equal(0, snapshot.Count);
        Assert.Empty(Decompress(snapshot.GzipBytes));
        Assert.Equal(64, snapshot.Sha256.Length);
    }

    [Fact]
    public void TheSha256MatchesTheActualBytes()
    {
        var snapshot = PlacesSnapshotExporter.Build([Place("Al Ezaby", 30.05, 31.24)]);

        var expected = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(snapshot.GzipBytes)).ToLowerInvariant();

        Assert.Equal(expected, snapshot.Sha256);
    }

    private static string[] Decompress(byte[] gzipBytes)
    {
        using var input = new MemoryStream(gzipBytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        var text = reader.ReadToEnd();
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }
}
