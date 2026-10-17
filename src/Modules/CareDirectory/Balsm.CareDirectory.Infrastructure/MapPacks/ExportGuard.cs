namespace Balsm.CareDirectory.Infrastructure.MapPacks;

/// <summary>
/// Decides whether a fresh places export is safe to publish.
///
/// A truncated export — a bug, a bad DB connection mid-query, an import that
/// silently returned partial results — must not overwrite last night's good
/// snapshot with a worse one. There is no signal that a drop is a bug rather
/// than a real change (a governorate does not lose half its pharmacies
/// overnight), so any large drop is refused rather than guessed about; the
/// previous snapshot stays live on the CDN and in the table until a run comes
/// back with a plausible count.
/// </summary>
public static class ExportGuard
{
    /// <summary>Below this fraction of the previous count, a snapshot is
    /// refused rather than published.</summary>
    public const double MinRetainFraction = 0.5;

    public static bool ShouldPublish(int previousCount, int newCount) =>
        previousCount <= 0 // nothing published yet for this governorate — any count is a first result, not a drop
            ? true
            : newCount >= previousCount * MinRetainFraction;
}
