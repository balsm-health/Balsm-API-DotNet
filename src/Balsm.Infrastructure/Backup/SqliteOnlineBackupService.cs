using System.Security.Cryptography;
using Balsm.Infrastructure.Configuration;
using Balsm.Infrastructure.Platform;
using Balsm.SharedKernel.Results;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsm.Infrastructure.Backup;

public sealed class SqliteOnlineBackupService : IBackupService
{
    private readonly DatabaseOptions _dbOptions;
    private readonly BackupOptions _backupOptions;
    private readonly PlatformDbContext _db;
    private readonly ILogger<SqliteOnlineBackupService> _logger;

    public SqliteOnlineBackupService(
        IOptions<DatabaseOptions> dbOptions,
        IOptions<BackupOptions> backupOptions,
        PlatformDbContext db,
        ILogger<SqliteOnlineBackupService> logger)
    {
        _dbOptions = dbOptions.Value;
        _backupOptions = backupOptions.Value;
        _db = db;
        _logger = logger;
    }

    public async Task<Result<BackupFile>> BackupNowAsync(
        BackupTrigger trigger,
        CancellationToken ct = default)
    {
        var dir = _backupOptions.Directory;
        System.IO.Directory.CreateDirectory(dir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var filename = $"balsm-{timestamp}.db";
        var destPath = System.IO.Path.Combine(dir, filename);

        try
        {
            _logger.LogInformation("Starting SQLite online backup: {Filename}", filename);

            await using var source = new SqliteConnection(_dbOptions.ConnectionString);
            await source.OpenAsync(ct).ConfigureAwait(false);

            await using var destination = new SqliteConnection($"Data Source={destPath}");
            await destination.OpenAsync(ct).ConfigureAwait(false);

            source.BackupDatabase(destination);

            await destination.CloseAsync().ConfigureAwait(false);
            await source.CloseAsync().ConfigureAwait(false);

            var sha256 = await ComputeSha256Async(destPath, ct).ConfigureAwait(false);
            var fileInfo = new System.IO.FileInfo(destPath);

            var backupFile = new BackupFile
            {
                Filename = filename,
                Path = destPath,
                SizeBytes = fileInfo.Length,
                Sha256 = sha256,
                Trigger = trigger,
                Status = BackupStatus.Completed
            };

            _db.BackupFiles.Add(backupFile);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Backup completed: {Filename} ({SizeBytes} bytes)", filename, fileInfo.Length);

            return Result.Success(backupFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed for {Filename}", filename);

            // Clean up partial file
            if (System.IO.File.Exists(destPath))
            {
                try { System.IO.File.Delete(destPath); } catch { /* best effort */ }
            }

            return Result.Failure<BackupFile>(
                new Error("Backup.Failed", $"Backup failed: {ex.Message}"));
        }
    }

    private static async Task<string> ComputeSha256Async(
        string filePath,
        CancellationToken ct)
    {
        await using var stream = System.IO.File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
