namespace Balsm.Geofence.Domain;

public interface IGeofenceService
{
    Task<bool> IsDeniedAsync(string countryCode, CancellationToken ct = default);
}
