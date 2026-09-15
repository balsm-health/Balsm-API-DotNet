using Balsm.SharedKernel.Results;

namespace Balsm.EmergencyQr.Domain;

/// <summary>Expected-failure catalog for the EmergencyQr context (Result pattern).</summary>
public static class EmergencyQrErrors
{
    /// <summary>Unknown jti OR a jti owned by another user — deliberately one
    /// error, so ownership cannot be probed (contract: 404).</summary>
    public static readonly Error NotFound = new("EmergencyQr.NotFound", "Token not found.");

    /// <summary>Revoked or expired token can no longer change (contract: 409).</summary>
    public static readonly Error TokenInactive = new("EmergencyQr.TokenInactive", "Token is revoked or expired.");

    /// <summary>ttl_seconds outside the allowed set (contract: 422 InvalidTtl).</summary>
    public static readonly Error InvalidTtl = new("EmergencyQr.InvalidTtl", "Invalid ttl_seconds value.");
}
