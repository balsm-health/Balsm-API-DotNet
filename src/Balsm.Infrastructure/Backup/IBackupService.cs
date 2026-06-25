using Balsm.Infrastructure.Platform;
using Balsm.SharedKernel.Results;

namespace Balsm.Infrastructure.Backup;

public interface IBackupService
{
    Task<Result<BackupFile>> BackupNowAsync(BackupTrigger trigger, CancellationToken ct = default);
}
