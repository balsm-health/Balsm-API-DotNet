namespace Balsm.Infrastructure.Backup;

public sealed class BackupOptions
{
    public const string SectionName = "Backup";
    public string Directory { get; set; } = "backups";
    public int MaxConcurrentBackups { get; set; } = 1;
}
