using System.Security.Cryptography;
using Balsm.Infrastructure.Configuration;
using Balsm.Infrastructure.Lifecycle;
using Balsm.Infrastructure.Platform;
using Balsm.SharedKernel.Results;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsm.Infrastructure.Backup;

public sealed class RestoreOrchestrator
{
    private readonly PlatformDbContext _db;
    private readonly DatabaseOptions _dbOptions;
    private readonly ReadinessGate _gate;
    private readonly ILogger<RestoreOrchestrator> _logger;

    public RestoreOrchestrator(
        PlatformDbContext db,
        IOptions<DatabaseOptions> dbOptions,
        ReadinessGate gate,
        ILogger<RestoreOrchestrator> logger)
    {
        _db = db;
        _dbOptions = dbOptions.Value;
        _gate = gate;
        _logger = logger;
    }

    public async Task<Result> RestoreAsync(Guid backupFileId, CancellationToken ct = default)
    {
        if (!_dbOptions.Provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(new Error(
                "Restore.Unsupported",
                "Database restore is only supported for SQLite deployments."));
        }

        // (a) Look up BackupFile row
        var backupFile = await _db.BackupFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == backupFileId && !b.IsDeleted, ct)
            .ConfigureAwait(false);

        if (backupFile is null)
            return Result.Failure(new Error("Restore.NotFound", "Backup file not found."));

        if (!System.IO.File.Exists(backupFile.Path))
            return Result.Failure(new Error("Restore.FileNotFound",
                $"Backup file not found on disk: {backupFile.Path}"));

        // Verify SHA-256
        var actualSha = await ComputeSha256Async(backupFile.Path, ct).ConfigureAwait(false);
        if (!string.Equals(actualSha, backupFile.Sha256, StringComparison.OrdinalIgnoreCase))
            return Result.Failure(new Error("Restore.ChecksumMismatch",
                "Backup file checksum does not match. The file may be corrupted."));

        // (b) Copy to .restoring
        var livePath = ExtractDbPath(_dbOptions.ConnectionString);
        var restoringPath = livePath + ".restoring";

        System.IO.File.Copy(backupFile.Path, restoringPath, overwrite: true);

        // (c) Run integrity check on the restore candidate
        var integrityError = await CheckIntegrityAsync(restoringPath, ct).ConfigureAwait(false);
        if (integrityError is not null)
        {
            System.IO.File.Delete(restoringPath);
            return Result.Failure(new Error("Restore.IntegrityFailed", integrityError));
        }

        // (d) Signal not-ready
        _gate.SetNotReady("restore");
        _logger.LogWarning(
            "Initiating database restore from backup {BackupId}. Service will restart.", backupFileId);

        // (e) Atomic file replace with rollback backup
        try
        {
            System.IO.File.Replace(restoringPath, livePath, livePath + ".rollback");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File.Replace failed during restore; rolling back");
            _gate.SetReady();
            if (System.IO.File.Exists(restoringPath))
                System.IO.File.Delete(restoringPath);
            return Result.Failure(new Error("Restore.ReplaceFailed", ex.Message));
        }

        _logger.LogInformation(
            "Database file replaced. Service readiness gate is closed — host should be restarted.");

        return Result.Success();
    }

    private static async Task<string?> CheckIntegrityAsync(string dbPath, CancellationToken ct)
    {
        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct).ConfigureAwait(false);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check; PRAGMA foreign_key_check;";

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        string? firstResult = null;
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var val = reader.GetString(0);
            if (firstResult is null) firstResult = val;
            if (!string.Equals(val, "ok", StringComparison.OrdinalIgnoreCase))
                return $"Integrity check failed: {val}";
        }

        return null;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = System.IO.File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ExtractDbPath(string connectionString)
    {
        // Parse "Data Source=path.db" or "Data Source=path.db;Mode=ReadWrite"
        foreach (var segment in connectionString.Split(';'))
        {
            var kv = segment.Split('=', 2);
            if (kv.Length == 2 && kv[0].Trim().Equals("Data Source", StringComparison.OrdinalIgnoreCase))
                return kv[1].Trim();
        }
        return "balsm.db";
    }
}
