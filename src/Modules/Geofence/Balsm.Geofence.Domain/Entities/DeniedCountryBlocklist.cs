namespace Balsm.Geofence.Domain.Entities;

public sealed class DeniedCountryBlocklist
{
    public string CountryCode { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty;
    public DateTime AddedAt { get; private set; } = DateTime.UtcNow;

    private DeniedCountryBlocklist() { }

    public static DeniedCountryBlocklist Create(string countryCode, string source) =>
        new() { CountryCode = countryCode.ToUpperInvariant(), Source = source };
}
