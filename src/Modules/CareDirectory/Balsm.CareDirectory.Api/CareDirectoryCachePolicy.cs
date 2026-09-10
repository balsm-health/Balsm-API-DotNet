namespace Balsm.CareDirectory.Api;

/// <summary>
/// Names for the care-directory output-cache policy.
///
/// The directory is public NON-PHI reference data that changes only when an
/// import runs, so it is the one response in this API safe to cache and serve
/// to any caller. Every other endpoint is patient-scoped and must not be.
/// </summary>
public static class CareDirectoryCachePolicy
{
    /// <summary>Policy name referenced by <c>[OutputCache]</c> on the controller.</summary>
    public const string Name = "care-directory";

    /// <summary>Tag for evicting the whole directory after a re-import.</summary>
    public const string Tag = "care-directory";
}
