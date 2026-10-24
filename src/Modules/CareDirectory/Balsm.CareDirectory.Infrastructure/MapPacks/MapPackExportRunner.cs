using Balsm.CareDirectory.Domain.Entities;
using Balsm.CareDirectory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Balsm.CareDirectory.Infrastructure.MapPacks;

/// <summary>
/// One nightly run: reconciles basemap rows against what is actually on the
/// CDN, then exports and publishes a fresh places snapshot per governorate.
///
/// Split out from <see cref="MapPackExportJob"/> (the BackgroundService/cron
/// wrapper) so the part with actual decisions in it can be tested against an
/// in-memory database and a fake bucket, with no timer and no network.
/// </summary>
public sealed class MapPackExportRunner(
    CareDirectoryDbContext db,
    IMapPackObjectStore store,
    IReadOnlyList<GovernorateRef> governorates,
    MapPackR2Options options,
    ILogger logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        await ReconcileBasemapsAsync(ct).ConfigureAwait(false);
        await ExportPlacesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// CI's publish.py uploads basemap bytes straight to R2 and never touches
    /// this database — it has no connection string and, per the design, no
    /// business asserting a row exists. This is the other half: the job that
    /// actually owns the table looks at what CI put on the CDN and catches
    /// the manifest up to it.
    /// </summary>
    private async Task ReconcileBasemapsAsync(CancellationToken ct)
    {
        var byId = governorates.ToDictionary(g => g.Id);
        var objects = await store.ListAsync("packs/", ct).ConfigureAwait(false);

        // A governorate can briefly have more than one .pmtiles key mid-
        // rollout of a new build; keep only the newest version (filenames
        // sort lexicographically by their yyyyMMdd date).
        var newestPerGovernorate = objects
            .Select(o => (Object: o, Parsed: BasemapObjectKey.Parse(o.Key)))
            .Where(x => x.Parsed is not null && byId.ContainsKey(x.Parsed!.Value.GovernorateId))
            .GroupBy(x => x.Parsed!.Value.GovernorateId)
            .Select(g => g.OrderByDescending(x => x.Parsed!.Value.Version).First());

        foreach (var (obj, parsed) in newestPerGovernorate)
        {
            var (id, version) = parsed!.Value;
            if (obj.Sha256Metadata is null)
            {
                // publish.py always sets this metadata; a key without it was
                // not written by that script and the app would reject a
                // download it cannot verify anyway.
                logger.LogWarning("Basemap object {Key} has no sha256 metadata — skipping", obj.Key);
                continue;
            }

            var governorate = byId[id];
            var url = $"{options.CdnBaseUrl.TrimEnd('/')}/{obj.Key}";

            var existing = await db.MapPackArtifacts
                .FirstOrDefaultAsync(a => a.GovernorateId == id && a.Kind == MapPackArtifactKind.Basemap, ct)
                .ConfigureAwait(false);

            if (existing is null)
            {
                db.MapPackArtifacts.Add(MapPackArtifact.Publish(
                    id, governorate.NameEn, governorate.NameAr, MapPackArtifactKind.Basemap,
                    version, obj.SizeBytes, obj.Sha256Metadata, url,
                    governorate.West, governorate.South, governorate.East, governorate.North));
            }
            else if (existing.Version != version)
            {
                existing.Republish(version, obj.SizeBytes, obj.Sha256Metadata, url, placeCount: null);
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task ExportPlacesAsync(CancellationToken ct)
    {
        var places = await db.CarePlaces.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        var version = DateTime.UtcNow.ToString("yyyyMMdd");

        foreach (var governorate in governorates)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await ExportOneGovernorateAsync(governorate, places, version, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // One governorate's failure — a bad R2 PUT, a transient DB
                // error — must not stop the other 26 from refreshing tonight.
                logger.LogError(ex, "Places export failed for {Governorate}", governorate.Id);
            }
        }
    }

    private async Task ExportOneGovernorateAsync(
        GovernorateRef governorate, IReadOnlyList<CarePlace> allPlaces, string version, CancellationToken ct)
    {
        var matched = allPlaces.Where(p => governorate.Contains(p.Lat, p.Lng)).ToList();
        var snapshot = PlacesSnapshotExporter.Build(matched);

        var existing = await db.MapPackArtifacts
            .FirstOrDefaultAsync(a => a.GovernorateId == governorate.Id && a.Kind == MapPackArtifactKind.Places, ct)
            .ConfigureAwait(false);

        var previousCount = existing?.PlaceCount ?? 0;
        if (!ExportGuard.ShouldPublish(previousCount, snapshot.Count))
        {
            logger.LogError(
                "Refusing places snapshot for {Governorate}: {New} places is a large drop from {Previous} — " +
                "publish skipped, previous snapshot stays live",
                governorate.Id, snapshot.Count, previousCount);
            return;
        }

        var key = $"places/{governorate.Id}-{version}.ndjson.gz";
        await store.PutAsync(key, snapshot.GzipBytes, "application/x-ndjson", snapshot.Sha256, ct)
            .ConfigureAwait(false);

        var url = $"{options.CdnBaseUrl.TrimEnd('/')}/{key}";

        if (existing is null)
        {
            db.MapPackArtifacts.Add(MapPackArtifact.Publish(
                governorate.Id, governorate.NameEn, governorate.NameAr, MapPackArtifactKind.Places,
                version, snapshot.GzipBytes.LongLength, snapshot.Sha256, url,
                governorate.West, governorate.South, governorate.East, governorate.North,
                placeCount: snapshot.Count));
        }
        else
        {
            existing.Republish(version, snapshot.GzipBytes.LongLength, snapshot.Sha256, url, snapshot.Count);
        }

        // Saved per governorate, not batched at the end of the loop: a
        // failure on governorate 15 must not roll back the 14 that already
        // succeeded and already have real bytes sitting on the CDN.
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
