using System.Text.Json;
using Balsm.CareDirectory.Domain.Entities;
using Balsm.CareDirectory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsm.CareDirectory.Infrastructure.MapPacks;

/// <summary>
/// Seeds the offline map-pack catalogue and exports places locally when Cloudflare R2
/// credentials are not configured (e.g. local development, standalone deployments).
///
/// In production with R2, MapPackExportJob runs nightly to reconcile basemaps and
/// upload places to R2. In local environments, this service seeds the 27 published
/// CDN basemaps and exports places snapshots from the local care_place table to
/// data/map-packs/places/*.ndjson.gz, allowing end-to-end testing of map pack
/// downloading without external cloud credentials.
/// </summary>
public sealed class LocalMapPackSeedService(
    IServiceScopeFactory scopeFactory,
    IOptions<MapPackR2Options> r2Options,
    IHostEnvironment environment,
    ILogger<LocalMapPackSeedService> logger) : IHostedService
{
    private const string BasemapsPath = "data/map-packs/basemaps.json";
    private const string GovernoratesPath = "data/map-packs/governorates.json";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (r2Options.Value.IsConfigured)
        {
            logger.LogInformation("Cloudflare R2 is configured — skipping LocalMapPackSeedService in favor of MapPackExportJob");
            return;
        }

        var governorates = LoadGovernorates();
        if (governorates.Count == 0)
        {
            logger.LogWarning("Local map-pack seed skipped: {Path} not found or empty", GovernoratesPath);
            return;
        }

        var basemaps = LoadBasemaps();
        if (basemaps.Count == 0)
        {
            logger.LogWarning("Local map-pack seed skipped: {Path} not found or empty", BasemapsPath);
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CareDirectoryDbContext>();

            await SeedBasemapsAsync(db, basemaps, cancellationToken).ConfigureAwait(false);
            await SeedPlacesAsync(db, governorates, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Local map-pack seed completed successfully for {Count} governorates", governorates.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Local map-pack seed failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedBasemapsAsync(CareDirectoryDbContext db, IReadOnlyList<BasemapSeedRow> basemaps, CancellationToken ct)
    {
        var existing = await db.MapPackArtifacts
            .Where(a => a.Kind == MapPackArtifactKind.Basemap)
            .ToDictionaryAsync(a => a.GovernorateId, ct)
            .ConfigureAwait(false);

        var added = 0;
        var updated = 0;

        foreach (var b in basemaps)
        {
            if (!existing.TryGetValue(b.Id, out var row))
            {
                db.MapPackArtifacts.Add(MapPackArtifact.Publish(
                    b.Id, b.NameEn, b.NameAr, MapPackArtifactKind.Basemap,
                    b.Version, b.SizeBytes, b.Sha256, b.Url,
                    b.Bounds[0], b.Bounds[1], b.Bounds[2], b.Bounds[3]));
                added++;
            }
            else if (row.Version != b.Version || row.Sha256 != b.Sha256)
            {
                row.Republish(b.Version, b.SizeBytes, b.Sha256, b.Url, placeCount: null);
                updated++;
            }
        }

        if (added > 0 || updated > 0)
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            logger.LogInformation("Seeded basemaps: {Added} added, {Updated} updated", added, updated);
        }
    }

    private async Task SeedPlacesAsync(CareDirectoryDbContext db, IReadOnlyList<GovernorateRef> governorates, CancellationToken ct)
    {
        var allPlaces = await db.CarePlaces.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        if (allPlaces.Count == 0)
        {
            logger.LogWarning("No care_place records found — skipping places export");
            return;
        }

        var placesDir = ResolvePlacesDirectory();
        Directory.CreateDirectory(placesDir);

        var existingPlaces = await db.MapPackArtifacts
            .Where(a => a.Kind == MapPackArtifactKind.Places)
            .ToDictionaryAsync(a => a.GovernorateId, ct)
            .ConfigureAwait(false);

        var version = DateTime.UtcNow.ToString("yyyyMMdd");
        var savedCount = 0;

        foreach (var gov in governorates)
        {
            ct.ThrowIfCancellationRequested();

            var filename = $"{gov.Id}-{version}.ndjson.gz";
            var filePath = Path.Combine(placesDir, filename);

            var baseDirPlaces = Path.Combine(AppContext.BaseDirectory, "data", "map-packs", "places");
            Directory.CreateDirectory(baseDirPlaces);
            var baseFilePath = Path.Combine(baseDirPlaces, filename);

            var matched = allPlaces.Where(p => gov.Contains(p.Lat, p.Lng)).ToList();
            var snapshot = PlacesSnapshotExporter.Build(matched);

            await File.WriteAllBytesAsync(filePath, snapshot.GzipBytes, ct).ConfigureAwait(false);
            if (baseFilePath != filePath)
            {
                await File.WriteAllBytesAsync(baseFilePath, snapshot.GzipBytes, ct).ConfigureAwait(false);
            }

            var url = $"/care/packs/places/{filename}";

            if (!existingPlaces.TryGetValue(gov.Id, out var row))
            {
                db.MapPackArtifacts.Add(MapPackArtifact.Publish(
                    gov.Id, gov.NameEn, gov.NameAr, MapPackArtifactKind.Places,
                    version, snapshot.GzipBytes.LongLength, snapshot.Sha256, url,
                    gov.West, gov.South, gov.East, gov.North,
                    placeCount: snapshot.Count));
                savedCount++;
            }
            else if (row.Version != version || row.Sha256 != snapshot.Sha256 || row.PlaceCount != snapshot.Count)
            {
                row.Republish(version, snapshot.GzipBytes.LongLength, snapshot.Sha256, url, snapshot.Count);
                savedCount++;
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Exported and seeded places for {Count} governorates ({Saved} written/updated)", governorates.Count, savedCount);
    }

    private string ResolvePlacesDirectory()
    {
        return Path.Combine(environment.ContentRootPath, "data", "map-packs", "places");
    }

    private IReadOnlyList<GovernorateRef> LoadGovernorates()
    {
        foreach (var root in new[] { AppContext.BaseDirectory, environment.ContentRootPath })
        {
            var candidate = Path.Combine(root, GovernoratesPath);
            if (File.Exists(candidate))
            {
                return GovernorateRegistry.Parse(File.ReadAllText(candidate));
            }
        }
        return [];
    }

    private IReadOnlyList<BasemapSeedRow> LoadBasemaps()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        foreach (var root in new[] { AppContext.BaseDirectory, environment.ContentRootPath })
        {
            var candidate = Path.Combine(root, BasemapsPath);
            if (File.Exists(candidate))
            {
                var content = File.ReadAllText(candidate);
                return JsonSerializer.Deserialize<List<BasemapSeedRow>>(content, options) ?? [];
            }
        }
        return [];
    }

    private sealed record BasemapSeedRow(
        string Id,
        string NameEn,
        string NameAr,
        string Version,
        long SizeBytes,
        string Sha256,
        string Url,
        List<double> Bounds);
}
