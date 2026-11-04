namespace Balsm.SharedKernel.Contracts;

/// <summary>
/// Published interface that turns a scanned profile-QR jti into the account
/// that owns it. Implemented by the EmergencyQr module and wired in the
/// composition root — Identity (family linking) consumes this contract instead
/// of touching EmergencyQr's DbContext (no cross-module project references, no
/// shared tables).
/// </summary>
public interface IProfileQrOwnerResolver
{
    /// <summary>
    /// The owner's user id when <paramref name="jti"/> names an active profile
    /// token; <c>null</c> for unknown, revoked and expired tokens alike, so a
    /// caller cannot tell those cases apart. Never records a scan.
    /// </summary>
    Task<Guid?> ResolveOwnerAsync(Guid jti, CancellationToken ct = default);
}
