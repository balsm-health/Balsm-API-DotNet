using Balsm.Infrastructure.Platform;
using Balsm.SharedKernel.Domain;

namespace Balsm.Infrastructure.Backup;

public sealed class BackupFile : BaseEntity
{
    public string Filename { get; set; } = "";
    public string Path { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public BackupTrigger Trigger { get; set; }
    public BackupStatus Status { get; set; }
}
