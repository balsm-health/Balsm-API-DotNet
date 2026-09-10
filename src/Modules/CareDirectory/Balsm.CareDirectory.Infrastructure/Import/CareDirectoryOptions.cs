namespace Balsm.CareDirectory.Infrastructure.Import;

/// <summary>
/// Import settings for the care directory. Bound from the "CareDirectory"
/// configuration section.
/// </summary>
public sealed class CareDirectoryOptions
{
    public const string SectionName = "CareDirectory";

    /// <summary>
    /// Import the artifact on host startup. The directory ships inside a
    /// self-hosted deployment, so an operator cannot be asked to run an import
    /// command by hand — a missed step would leave the map empty.
    /// </summary>
    public bool ImportOnStartup { get; set; } = true;

    /// <summary>Artifact path, relative to the content root.</summary>
    public string ArtifactPath { get; set; } = "data/care-directory/care_places.eg.ndjson.gz";

    /// <summary>
    /// Rows below this confidence are skipped. Overture's category taxonomy is
    /// noisy — a domestic-staffing agency is typed `clinic` at 0.62 — and
    /// confidence is the only quality signal available, since operating_status is
    /// NULL for every Egyptian row.
    /// </summary>
    public double MinConfidence { get; set; } = 0.65;

    /// <summary>
    /// Per-type floors. One global threshold serves the types badly in opposite
    /// directions: at 0.65 pharmacy loses 62% of its rows while scan is left with
    /// 177 nationwide.
    /// </summary>
    public Dictionary<string, double> MinConfidenceByType { get; set; } = new();

    public double FloorFor(string type) =>
        MinConfidenceByType.TryGetValue(type, out var floor) ? floor : MinConfidence;
}
