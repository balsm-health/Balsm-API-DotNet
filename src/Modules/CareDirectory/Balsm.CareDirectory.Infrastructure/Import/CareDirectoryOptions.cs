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
    /// Rows below this confidence are skipped.
    ///
    /// Confidence is the only quality signal available — operating_status is NULL
    /// for every Egyptian row — but it is a weak one, and an aggressive floor
    /// costs far more than it saves. Sampling the 0.40-0.65 band found it
    /// overwhelmingly legitimate: named pharmacies, a physiotherapy centre, an
    /// eye-surgery clinic, dental practices. A 0.65 floor discarded 15,930 such
    /// rows to exclude a minority of miscategorised ones, leaving whole cities
    /// looking empty.
    ///
    /// 0.40 keeps ~34,800 of 38,395. Below it the junk concentrates, and the
    /// remaining miscategorisation is better addressed by category and name rules
    /// than by throwing away real facilities.
    /// </summary>
    public double MinConfidence { get; set; } = 0.40;

    /// <summary>
    /// Per-type floors. One global threshold serves the types badly in opposite
    /// directions: at 0.65 pharmacy loses 62% of its rows while scan is left with
    /// 177 nationwide.
    /// </summary>
    public Dictionary<string, double> MinConfidenceByType { get; set; } = new();

    public double FloorFor(string type) =>
        MinConfidenceByType.TryGetValue(type, out var floor) ? floor : MinConfidence;
}
