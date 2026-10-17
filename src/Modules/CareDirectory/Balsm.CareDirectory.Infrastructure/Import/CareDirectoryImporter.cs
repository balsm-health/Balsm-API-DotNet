using System.IO.Compression;
using System.Text.Json;
using Balsm.CareDirectory.Domain;
using Balsm.CareDirectory.Domain.Entities;
using Balsm.CareDirectory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Balsm.CareDirectory.Infrastructure.Import;

/// <summary>Outcome of one import run.</summary>
public sealed record ImportResult(int Inserted, int Updated, int Skipped);

/// <summary>One line of the extract artifact. Wire keys are snake_case.</summary>
internal sealed record CarePlaceRecord(
    string ExternalId,
    string Type,
    string Name,
    string NameScript,
    string? Address,
    double Lat,
    double Lng,
    string? Phone,
    double Confidence,
    string? Brand,
    string Source);

/// <summary>
/// Loads the Overture extract artifact into care_place.
///
/// Idempotent: rows upsert on the Overture GERS id, so a re-import refreshes in
/// place and never duplicates. That identity is what lets anything keyed to a
/// row — curated overrides, user corrections — survive a monthly refresh.
/// </summary>
public sealed class CareDirectoryImporter(CareDirectoryDbContext db, CareDirectoryOptions options)
{
    private const int BatchSize = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public async Task<ImportResult> ImportAsync(Stream artifact, CancellationToken ct)
    {
        // One lookup of what is already stored beats a query per row; the whole
        // Egyptian directory is ~38k rows, which is comfortably in memory.
        var existing = await db.CarePlaces
            .Where(p => p.ExternalId != null)
            .ToDictionaryAsync(p => p.ExternalId!, ct)
            .ConfigureAwait(false);

        int inserted = 0, updated = 0, skipped = 0, pending = 0;

        using var reader = new StreamReader(artifact);
        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var record = JsonSerializer.Deserialize<CarePlaceRecord>(line, JsonOptions);
            if (record is null || record.Confidence < options.FloorFor(record.Type))
            {
                skipped++;
                continue;
            }

            var (nameEn, nameAr) = ResolveNames(record);
            if (nameEn is null && nameAr is null)
            {
                skipped++;
                continue;
            }

            var (addressEn, addressAr) = ResolveAddress(record.Address);

            if (existing.TryGetValue(record.ExternalId, out var row))
            {
                row.UpdateFromImport(record.Type, nameEn, nameAr, addressEn, addressAr,
                    record.Lat, record.Lng, record.Phone, record.Confidence);
                updated++;
            }
            else
            {
                db.CarePlaces.Add(CarePlace.Create(
                    type: record.Type,
                    nameEn: nameEn,
                    nameAr: nameAr,
                    addressEn: addressEn,
                    addressAr: addressAr,
                    lat: record.Lat,
                    lng: record.Lng,
                    countryCode: "EG",
                    source: record.Source,
                    externalId: record.ExternalId,
                    phone: record.Phone,
                    confidence: record.Confidence));
                inserted++;
            }

            if (++pending >= BatchSize)
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                pending = 0;
            }
        }

        if (pending > 0)
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return new ImportResult(inserted, updated, skipped);
    }

    // Overture carries one name per place. Where that single string holds both
    // scripts — 14% of Egyptian listings do — recover the pair; otherwise file the
    // name under its own script and leave the other side null rather than
    // transliterating, because a mangled hospital name is worse than an absent one.
    private static (string? English, string? Arabic) ResolveNames(CarePlaceRecord record)
    {
        if (BilingualName.TrySplit(record.Name, out var english, out var arabic))
        {
            return (english, arabic);
        }

        return record.NameScript == "ar" || BilingualName.IsArabic(record.Name)
            ? (null, record.Name)
            : (record.Name, null);
    }

    private static (string? English, string? Arabic) ResolveAddress(string? address)
    {
        if (address is null)
        {
            return (null, null);
        }

        if (BilingualName.TrySplit(address, out var english, out var arabic))
        {
            return (english, arabic);
        }

        return BilingualName.IsArabic(address) ? (null, address) : (address, null);
    }

    /// <summary>Opens the artifact, transparently decompressing a .gz path.</summary>
    public static Stream Open(string path)
    {
        var file = File.OpenRead(path);
        return path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(file, CompressionMode.Decompress)
            : file;
    }
}
