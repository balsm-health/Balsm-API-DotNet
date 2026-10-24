using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Balsm.CareDirectory.Domain.Entities;

namespace Balsm.CareDirectory.Infrastructure.MapPacks;

/// <summary>Result of building one governorate's places snapshot.</summary>
public sealed record ExportedSnapshot(byte[] GzipBytes, string Sha256, int Count);

/// <summary>
/// Builds the gzipped ndjson snapshot uploaded for one governorate's places.
///
/// One JSON object per line, the same field names and casing as
/// GET /care/entities (id, type, name_en, name_ar, address_en, address_ar,
/// lat, lng, hours, phone, rating) — deliberately, so the app's online parser
/// can read an offline snapshot line without a second model. distance_km is
/// absent: it is relative to a viewer position that does not exist yet when
/// this file is built.
/// </summary>
public static class PlacesSnapshotExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static ExportedSnapshot Build(IEnumerable<CarePlace> places)
    {
        var buffer = new MemoryStream();
        var count = 0;

        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        using (var writer = new StreamWriter(gzip, Encoding.UTF8))
        {
            foreach (var place in places)
            {
                var record = new PlaceRecord(
                    place.Id, place.Type, place.NameEn, place.NameAr,
                    place.AddressEn, place.AddressAr, place.Lat, place.Lng,
                    place.Hours, place.Phone, place.Rating);
                writer.WriteLine(JsonSerializer.Serialize(record, JsonOptions));
                count++;
            }
        }

        var bytes = buffer.ToArray();
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new ExportedSnapshot(bytes, sha256, count);
    }

    private sealed record PlaceRecord(
        Guid Id, string Type, string? NameEn, string? NameAr,
        string? AddressEn, string? AddressAr, double Lat, double Lng,
        string? Hours, string? Phone, double? Rating);
}
