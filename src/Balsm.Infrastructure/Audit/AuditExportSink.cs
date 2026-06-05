using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Balsm.Infrastructure.Platform;

namespace Balsm.Infrastructure.Audit;

public sealed class AuditExportSink
{
    private readonly PlatformDbContext _db;

    public AuditExportSink(PlatformDbContext db)
    {
        _db = db;
    }

    public async Task<AuditArchive> WriteAsync(
        IEnumerable<AuditLog> rows,
        string filename,
        CancellationToken ct = default)
    {
        var rowList = rows.ToList();
        var dir = System.IO.Path.GetDirectoryName(filename);
        if (!string.IsNullOrEmpty(dir))
            System.IO.Directory.CreateDirectory(dir);

        // Write JSONL
        await using var fileStream = new System.IO.FileStream(
            filename,
            System.IO.FileMode.Create,
            System.IO.FileAccess.Write,
            System.IO.FileShare.None,
            bufferSize: 65536,
            useAsync: true);

        await using var writer = new System.IO.StreamWriter(fileStream, Encoding.UTF8);
        foreach (var row in rowList)
        {
            var json = JsonSerializer.Serialize(row);
            await writer.WriteLineAsync(json.AsMemory(), ct).ConfigureAwait(false);
        }

        await writer.FlushAsync(ct).ConfigureAwait(false);
        await fileStream.FlushAsync(ct).ConfigureAwait(false);

        // fsync
        fileStream.Flush(flushToDisk: true);

        var fileInfo = new System.IO.FileInfo(filename);
        var sha256 = await ComputeSha256Async(filename, ct).ConfigureAwait(false);

        var periodStart = rowList.Count > 0
            ? rowList.Min(r => r.OccurredAt)
            : DateTime.UtcNow;
        var periodEnd = rowList.Count > 0
            ? rowList.Max(r => r.OccurredAt)
            : DateTime.UtcNow;

        var archive = new AuditArchive
        {
            Filename = System.IO.Path.GetFileName(filename),
            Path = filename,
            SizeBytes = fileInfo.Length,
            Sha256 = sha256,
            ArchivedAt = DateTime.UtcNow,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            RowCount = rowList.Count
        };

        _db.AuditArchives.Add(archive);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return archive;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = System.IO.File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
