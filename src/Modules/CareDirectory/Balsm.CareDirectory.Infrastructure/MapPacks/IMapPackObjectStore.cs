namespace Balsm.CareDirectory.Infrastructure.MapPacks;

/// <summary>One object found under a prefix.</summary>
public sealed record R2Object(string Key, long SizeBytes, string? Sha256Metadata);

/// <summary>
/// The bucket the nightly job reads basemap keys from and writes places
/// snapshots to. A thin seam over R2 purely so <c>MapPackExportRunner</c>'s
/// upsert and guard logic can be tested against a fake, in-memory bucket
/// instead of real credentials and a network call.
/// </summary>
public interface IMapPackObjectStore
{
    Task<IReadOnlyList<R2Object>> ListAsync(string prefix, CancellationToken ct);

    Task PutAsync(string key, byte[] content, string contentType, string sha256, CancellationToken ct);
}
