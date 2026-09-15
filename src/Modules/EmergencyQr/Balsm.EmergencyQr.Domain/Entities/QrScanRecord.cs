using Balsm.SharedKernel.Domain;

namespace Balsm.EmergencyQr.Domain.Entities;

/// <summary>
/// One successful public resolve of a QR token (spec v2.0 scan history).
/// Records WHEN and roughly WHAT KIND of client scanned — never who: the
/// resolve surface is anonymous and the scanner's identity is not collected.
/// Failed resolves (unknown/revoked/expired jti) are not recorded.
/// </summary>
public sealed class QrScanRecord : AggregateRoot
{
    public Guid TokenId { get; private set; }

    /// <summary>Token owner, denormalised so the owner's history query never
    /// joins through a revoked token row.</summary>
    public Guid OwnerUserId { get; private set; }

    public DateTime ResolvedAt { get; private set; }

    /// <summary>Coarse client class parsed from the User-Agent: "web", "app",
    /// or "unknown". Never the raw User-Agent string.</summary>
    public string ClientClass { get; private set; } = "unknown";

    /// <summary>ISO country code when coarse geo is available; null otherwise.
    /// Populated only when a GeoIP source is configured — never stored at
    /// finer granularity than country.</summary>
    public string? Country { get; private set; }

    private QrScanRecord() { }

    public static QrScanRecord Record(Guid tokenId, Guid ownerUserId, string clientClass, string? country = null)
        => new()
        {
            Id = Guid.NewGuid(),
            TokenId = tokenId,
            OwnerUserId = ownerUserId,
            ResolvedAt = DateTime.UtcNow,
            ClientClass = clientClass,
            Country = country
        };
}
