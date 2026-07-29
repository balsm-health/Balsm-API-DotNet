namespace Balsm.CareDirectory.Domain.Entities;

/// <summary>
/// A public, NON-PHI "nearby health place" (hospital, clinic, pharmacy, lab,
/// scan, store) surfaced by the patient-app care directory. Plain POCO — the
/// directory is reference data, not a domain aggregate, so it deliberately does
/// NOT extend BaseEntity.
/// </summary>
public sealed class CarePlace
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string AddressEn { get; private set; } = string.Empty;
    public string AddressAr { get; private set; } = string.Empty;
    public double Lat { get; private set; }
    public double Lng { get; private set; }
    public string Hours { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public double Rating { get; private set; }
    public string CountryCode { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private CarePlace() { }

    public static CarePlace Create(
        string type,
        string nameEn,
        string nameAr,
        string addressEn,
        string addressAr,
        double lat,
        double lng,
        string hours,
        string phone,
        double rating,
        string countryCode) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            NameEn = nameEn,
            NameAr = nameAr,
            AddressEn = addressEn,
            AddressAr = addressAr,
            Lat = lat,
            Lng = lng,
            Hours = hours,
            Phone = phone,
            Rating = rating,
            CountryCode = countryCode,
            CreatedAt = DateTime.UtcNow
        };
}
