using System.Text.RegularExpressions;

namespace Balsm.CareDirectory.Infrastructure.MapPacks;

/// <summary>
/// Parses a basemap object key written by tools/map-packs/publish.py
/// ("packs/{governorate-id}-{yyyyMMdd}.pmtiles") back into its parts.
///
/// A parse failure is not exceptional: the bucket can hold objects this job
/// has no business touching (a stray upload, a future artifact kind), so a
/// non-matching key is skipped by its caller rather than treated as a corrupt
/// manifest.
/// </summary>
public static partial class BasemapObjectKey
{
    // Governorate ids are lowercase letters and hyphens only (no digits), so
    // the greedy id group backtracks to the right boundary in front of the
    // literal "-{8 digits}.pmtiles" without ambiguity, even for hyphenated
    // ids like "beni-suef" or "kafr-el-sheikh".
    [GeneratedRegex(@"^packs/(?<id>[a-z-]+)-(?<version>\d{8})\.pmtiles$")]
    private static partial Regex Pattern();

    public static (string GovernorateId, string Version)? Parse(string key)
    {
        var match = Pattern().Match(key);
        return match.Success
            ? (match.Groups["id"].Value, match.Groups["version"].Value)
            : null;
    }
}
