using Balsm.SharedKernel.Domain;

namespace Balsm.Infrastructure.Audit;

public sealed class AuditArchive : BaseEntity
{
    public string Filename { get; set; } = "";
    public string Path { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public DateTime ArchivedAt { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int RowCount { get; set; }
}
