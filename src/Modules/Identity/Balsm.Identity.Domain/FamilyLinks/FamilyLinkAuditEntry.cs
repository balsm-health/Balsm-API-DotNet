namespace Balsm.Identity.Domain.FamilyLinks;

/// <summary>
/// Immutable audit row for every family-link transition and every read of a
/// sealed card: who, when, what, from which status to which. Carries no PHI —
/// ids, action names and statuses only.
/// </summary>
public sealed class FamilyLinkAuditEntry
{
    public Guid Id { get; private set; }
    public Guid FamilyLinkId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public FamilyLinkAuditAction Action { get; private set; }
    public FamilyLinkStatus? FromStatus { get; private set; }
    public FamilyLinkStatus ToStatus { get; private set; }
    public string? CorrelationId { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private FamilyLinkAuditEntry() { }

    public static FamilyLinkAuditEntry Record(
        Guid linkId,
        Guid actorUserId,
        FamilyLinkAuditAction action,
        FamilyLinkStatus? fromStatus,
        FamilyLinkStatus toStatus,
        string? correlationId,
        DateTime now) => new()
        {
            Id = Guid.NewGuid(),
            FamilyLinkId = linkId,
            ActorUserId = actorUserId,
            Action = action,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            CorrelationId = correlationId,
            OccurredAt = now,
        };
}
