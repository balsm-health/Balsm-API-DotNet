using Balsm.SharedKernel.Domain;

namespace Balsm.Infrastructure.Audit;

public sealed class AuditLog : BaseEntity
{
    public long Sequence { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Actor { get; set; }
    public string? SourceIp { get; set; }
    public string Module { get; set; } = "";
    public string Action { get; set; } = "";
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? DetailsJson { get; set; }
    public string? CorrelationId { get; set; }
}
