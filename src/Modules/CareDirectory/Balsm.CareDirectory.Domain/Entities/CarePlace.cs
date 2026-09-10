namespace Balsm.CareDirectory.Domain.Entities;

/// <summary>
/// A public, NON-PHI "nearby health place" (hospital, clinic, dentist, pharmacy,
/// lab, scan, store) surfaced by the patient-app care directory. Plain POCO —
/// the directory is reference data, not a domain aggregate, so it deliberately
/// does NOT extend BaseEntity.
///
/// Most optional fields are optional because no lawful source supplies them:
/// Overture has no opening-hours or ratings field at all, carries one name per
/// place rather than a bilingual pair, and omits a phone for roughly 8% of rows.
/// They are nullable so the directory never has to invent a value — the failure
/// this entity's fabricated seed used to embody.
/// </summary>
public sealed class CarePlace
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;

    /// <summary>English name. Null when the upstream listing is Arabic-only.</summary>
    public string? NameEn { get; private set; }

    /// <summary>Arabic name. Null when the upstream listing is Latin-only.</summary>
    public string? NameAr { get; private set; }

    public string? AddressEn { get; private set; }
    public string? AddressAr { get; private set; }

    public double Lat { get; private set; }
    public double Lng { get; private set; }

    /// <summary>Opening hours. Always null from Overture, which has no such field.</summary>
    public string? Hours { get; private set; }

    public string? Phone { get; private set; }

    /// <summary>Rating out of 5. No lawful free source supplies one; reserved for future user ratings.</summary>
    public double? Rating { get; private set; }

    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>Overture GERS id — the upsert key that lets curated values survive a re-import. Null for curated-only rows.</summary>
    public string? ExternalId { get; private set; }

    /// <summary>Provenance: overture | osm | curated | user.</summary>
    public string Source { get; private set; } = "curated";

    /// <summary>Upstream confidence, retained so the import floor can be retuned without re-extracting.</summary>
    public double? Confidence { get; private set; }

    /// <summary>Search-normalised Arabic name. Maintained here so it can never drift from its source column.</summary>
    public string? NameArNorm { get; private set; }

    /// <summary>Search-normalised Arabic address. See <see cref="NameArNorm"/>.</summary>
    public string? AddressArNorm { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private CarePlace() { }

    public static CarePlace Create(
        string type,
        string? nameEn,
        string? nameAr,
        string? addressEn,
        string? addressAr,
        double lat,
        double lng,
        string countryCode,
        string source,
        string? externalId = null,
        string? hours = null,
        string? phone = null,
        double? rating = null,
        double? confidence = null)
    {
        RequireAName(nameEn, nameAr);

        var now = DateTime.UtcNow;
        return new CarePlace
        {
            Id = Guid.NewGuid(),
            Type = type,
            NameEn = nameEn,
            NameAr = nameAr,
            AddressEn = addressEn,
            AddressAr = addressAr,
            NameArNorm = ArabicText.Normalize(nameAr),
            AddressArNorm = ArabicText.Normalize(addressAr),
            Lat = lat,
            Lng = lng,
            Hours = hours,
            Phone = phone,
            Rating = rating,
            CountryCode = countryCode,
            ExternalId = externalId,
            Source = source,
            Confidence = confidence,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Refreshes an existing row from a re-import. Deliberately leaves
    /// <see cref="Id"/> and <see cref="CreatedAt"/> alone: the row's identity and
    /// age survive the refresh, which is what makes anything keyed to this row —
    /// curated overrides, user reports — stable across monthly re-imports.
    /// </summary>
    public void UpdateFromImport(
        string type,
        string? nameEn,
        string? nameAr,
        string? addressEn,
        string? addressAr,
        double lat,
        double lng,
        string? phone,
        double? confidence)
    {
        RequireAName(nameEn, nameAr);

        Type = type;
        NameEn = nameEn;
        NameAr = nameAr;
        AddressEn = addressEn;
        AddressAr = addressAr;
        NameArNorm = ArabicText.Normalize(nameAr);
        AddressArNorm = ArabicText.Normalize(addressAr);
        Lat = lat;
        Lng = lng;
        Phone = phone;
        Confidence = confidence;
        UpdatedAt = DateTime.UtcNow;
    }

    // Both name columns are nullable, but a place with neither is unrenderable —
    // it would surface in the app as a blank row on the map.
    private static void RequireAName(string? nameEn, string? nameAr)
    {
        if (string.IsNullOrWhiteSpace(nameEn) && string.IsNullOrWhiteSpace(nameAr))
        {
            throw new ArgumentException("A care place needs at least one of nameEn or nameAr.");
        }
    }
}
