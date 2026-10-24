namespace Balsm.CareDirectory.Domain.Entities;

/// <summary>Which downloadable artifact a row describes.</summary>
public enum MapPackArtifactKind
{
    /// <summary>Vector basemap tiles (.pmtiles). Rebuilt monthly by CI.</summary>
    Basemap = 0,

    /// <summary>A dated snapshot of a governorate's places. Rebuilt nightly.</summary>
    Places = 1,
}

/// <summary>
/// One downloadable offline artifact for one governorate.
///
/// Basemaps and places are separate rows because they change at completely
/// different rates and sizes — a governorate's places are roughly 1% of its
/// basemap (Cairo: 1.0 MB against 25 MB), and the provider list moves nightly
/// while street geometry barely moves at all. Versioning them together would
/// mean either rebuilding 297 MB to refresh 3 MB, or letting places go stale
/// at the basemap's cadence.
///
/// A row exists only for an artifact that is already on the CDN: the job that
/// uploads is the job that writes this, in that order. That is the whole point
/// of holding the manifest here rather than reading it from object storage on
/// the request path — the manifest cannot describe a file that is not there.
/// </summary>
public sealed class MapPackArtifact
{
    private MapPackArtifact() { }

    public Guid Id { get; private set; }

    /// <summary>Stable governorate slug ("cairo"). Keyed on rather than a name,
    /// which is translated and may be re-spelled upstream.</summary>
    public string GovernorateId { get; private set; } = string.Empty;

    public string NameEn { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;

    public MapPackArtifactKind Kind { get; private set; }

    /// <summary>YYYYMMDD. For a basemap, the date of the OSM data it contains;
    /// for places, the night it was exported.</summary>
    public string Version { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;

    /// <summary>Places only — shown before download so the size means
    /// something, and the signal a truncated export would disturb.</summary>
    public int? PlaceCount { get; private set; }

    // Governorate extent, [west, south, east, north]. Stored per row and not in
    // a governorate table: 27 rows of reference data do not earn a join, and
    // the pipeline reads the extent straight out of the archive it just cut.
    public double West { get; private set; }
    public double South { get; private set; }
    public double East { get; private set; }
    public double North { get; private set; }

    public DateTime PublishedAt { get; private set; }

    public static MapPackArtifact Publish(
        string governorateId,
        string nameEn,
        string nameAr,
        MapPackArtifactKind kind,
        string version,
        long sizeBytes,
        string sha256,
        string url,
        double west,
        double south,
        double east,
        double north,
        int? placeCount = null)
    {
        if (string.IsNullOrWhiteSpace(governorateId)) throw new ArgumentException("governorate id is required", nameof(governorateId));
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("version is required", nameof(version));
        if (sizeBytes <= 0) throw new ArgumentException("an artifact with no bytes is not publishable", nameof(sizeBytes));
        // A 64-char hex digest, checked here because the app rejects a download
        // whose checksum does not match — a malformed one would fail every
        // install rather than none.
        if (sha256?.Length != 64) throw new ArgumentException("sha256 must be 64 hex characters", nameof(sha256));
        if (kind == MapPackArtifactKind.Places && placeCount is null or < 0)
            throw new ArgumentException("a places artifact must carry its count", nameof(placeCount));

        return new MapPackArtifact
        {
            Id = Guid.NewGuid(),
            GovernorateId = governorateId,
            NameEn = nameEn,
            NameAr = nameAr,
            Kind = kind,
            Version = version,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            Url = url,
            PlaceCount = placeCount,
            West = west,
            South = south,
            East = east,
            North = north,
            PublishedAt = DateTime.UtcNow,
        };
    }

    /// <summary>Replaces this row with a newer build of the same artifact.</summary>
    public void Republish(string version, long sizeBytes, string sha256, string url, int? placeCount)
    {
        Version = version;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        Url = url;
        PlaceCount = placeCount;
        PublishedAt = DateTime.UtcNow;
    }
}
