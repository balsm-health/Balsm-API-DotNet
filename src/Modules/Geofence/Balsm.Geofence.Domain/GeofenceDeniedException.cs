namespace Balsm.Geofence.Domain;

public sealed class GeofenceDeniedException(string countryCode)
    : Exception($"Country '{countryCode}' is blocked by OFAC sanctions list");
