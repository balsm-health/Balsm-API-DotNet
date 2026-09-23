using Balsm.SharedKernel.Results;

namespace Balsm.CareTeam.Domain;

/// <summary>Expected-failure catalog for the CareTeam context (Result pattern).</summary>
public static class CareTeamErrors
{
    /// <summary>Unknown id OR an id owned by another user — deliberately one
    /// error so ownership cannot be probed (contract: 404).</summary>
    public static readonly Error NotFound = new("CareTeam.NotFound", "Care provider not found.");

    /// <summary>Upsert targeting an id the server has already tombstoned
    /// (contract: 409). A stale device must pull the tombstone, not push over it.</summary>
    public static readonly Error Tombstoned = new("CareTeam.Tombstoned", "Care provider was deleted.");

    /// <summary>type outside AllowedTypes (contract: 422).</summary>
    public static readonly Error InvalidType = new("CareTeam.InvalidType", "Invalid care provider type.");
}
