using System.Text.Json;

namespace Balsm.CareDirectory.Infrastructure.MapPacks;

/// <summary>
/// One governorate's identity and extent — the reference list the nightly
/// export job iterates, and the basemap-reconciliation step matches CDN keys
/// against. See data/map-packs/README.md for where this data comes from.
/// </summary>
public sealed record GovernorateRef(
    string Id,
    string NameEn,
    string NameAr,
    double West,
    double South,
    double East,
    double North)
{
    /// <summary>
    /// Bounding-box membership. An approximation of the true admin polygon —
    /// a place near a governorate border can fall on the wrong side, or, in
    /// the narrow strip where two rectangles overlap, on both. Accepted for
    /// the same reason the manifest stores a bbox rather than a polygon:
    /// re-partitioning ~38k rows against 27 real polygons every night is real
    /// work for an edge case that affects a handful of border pins, not
    /// whether a governorate's pack is offerable at all.
    /// </summary>
    public bool Contains(double lat, double lng) =>
        lat >= South && lat <= North && lng >= West && lng <= East;
}

/// <summary>Parses data/map-packs/governorates.json.</summary>
public static class GovernorateRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static IReadOnlyList<GovernorateRef> Parse(string json)
    {
        var rows = JsonSerializer.Deserialize<List<Row>>(json, JsonOptions);
        if (rows is null) return [];

        return rows
            .Select(r => new GovernorateRef(r.Id, r.NameEn, r.NameAr, r.West, r.South, r.East, r.North))
            .ToList();
    }

    private sealed record Row(
        string Id, string NameEn, string NameAr, double West, double South, double East, double North);
}
