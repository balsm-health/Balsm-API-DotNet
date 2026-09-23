using Balsm.SharedKernel.Domain;

namespace Balsm.CareTeam.Domain.Entities;

/// <summary>
/// One row per request that decrypted care-team ciphertext (FR-504), mirroring
/// the FR-048 pattern for DOB. Holds no PHI itself — only who read how much.
/// </summary>
public sealed class CareTeamAuditLog : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid HealthProfileId { get; private set; }
    public string? Actor { get; private set; }
    public string? SourceIp { get; private set; }
    public string? CorrelationId { get; private set; }
    public int RowCount { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private CareTeamAuditLog() { }

    public static CareTeamAuditLog Record(
        Guid userId, Guid healthProfileId, string? actor, string? sourceIp,
        string? correlationId, int rowCount) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        HealthProfileId = healthProfileId,
        Actor = actor,
        SourceIp = sourceIp,
        CorrelationId = correlationId,
        RowCount = rowCount,
        OccurredAt = DateTime.UtcNow
    };
}
