using Balsm.SharedKernel.Results;
using Balsm.Infrastructure.Platform;
using Microsoft.Extensions.Logging;

namespace Balsm.Infrastructure.Backup;

public sealed class UnsupportedBackupService(ILogger<UnsupportedBackupService> logger) : IBackupService
{
    public Task<Result<BackupFile>> BackupNowAsync(
        BackupTrigger trigger,
        CancellationToken ct = default)
    {
        logger.LogWarning(
            "Backup requested for a non-SQLite deployment. Trigger: {Trigger}",
            trigger);

        return Task.FromResult(Result.Failure<BackupFile>(new Error(
            "Backup.Unsupported",
            "Database backups are only supported for SQLite deployments.")));
    }
}
