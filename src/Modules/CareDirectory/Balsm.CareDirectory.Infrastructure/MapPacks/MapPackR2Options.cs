namespace Balsm.CareDirectory.Infrastructure.MapPacks;

/// <summary>
/// R2 credentials and cron for the nightly map-pack export job.
///
/// Bound from flat environment variables, not a nested config section, so the
/// account/key/secret/bucket names line up with what
/// tools/map-packs/publish.py and its CI workflow already use as GitHub
/// secrets (R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY, R2_BUCKET).
/// Never hardcoded, never logged.
///
/// <see cref="CdnBaseUrl"/> is a separate variable from CI's PACKS_BASE_URL
/// on purpose: PACKS_BASE_URL already has "/packs" baked in (it is appended
/// directly to a pack filename), whereas this job builds both "packs/…" and
/// "places/…" keys, so it needs the bucket's bare public root instead.
/// </summary>
public sealed class MapPackR2Options
{
    public string AccountId { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;

    /// <summary>Bucket's public root, e.g. "https://cdn.balsm.health" — no
    /// trailing slash, no "/packs" suffix. The job appends the R2 key, which
    /// already carries its own "packs/" or "places/" prefix.</summary>
    public string CdnBaseUrl { get; set; } = string.Empty;

    /// <summary>UTC cron for the nightly run. Independent of the basemap CI
    /// schedule — the two artifacts version independently and neither run
    /// depends on the other completing.</summary>
    public string ExportCron { get; set; } = "0 1 * * *";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountId)
        && !string.IsNullOrWhiteSpace(AccessKeyId)
        && !string.IsNullOrWhiteSpace(SecretAccessKey)
        && !string.IsNullOrWhiteSpace(Bucket)
        && !string.IsNullOrWhiteSpace(CdnBaseUrl);
}
