using Balsm.SharedKernel.Events;

namespace Balsm.EmergencyQr.Domain.Events;

/// <summary>A QR token became active (fresh mint — not the idempotent
/// ciphertext refresh of an existing token).</summary>
public sealed record EmergencyQrTokenMinted(Guid TokenId, Guid UserId, bool IsPermanent) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

/// <summary>A QR token stopped resolving. Downstream contexts react here
/// (audit trail, cache invalidation) instead of reading this module's tables.</summary>
public sealed record EmergencyQrTokenRevoked(Guid TokenId, Guid UserId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

/// <summary>A successful public resolve — the hook for the spec v2.0 scan
/// push notification (phase 2) without coupling to the scan-history table.</summary>
public sealed record EmergencyQrTokenScanned(Guid TokenId, Guid OwnerUserId, string ClientClass) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
