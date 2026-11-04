using Balsm.Identity.Domain.FamilyLinks;

namespace Balsm.Identity.Tests;

/// <summary>Fixed, synthetic ids and bytes — no real accounts, no PHI.</summary>
internal static class FamilyLinkFixtures
{
    public static readonly Guid LinkId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    public static readonly Guid Requester = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid Recipient = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid Stranger = Guid.Parse("00000000-0000-0000-0000-000000000003");
    public static readonly Guid Jti = Guid.Parse("00000000-0000-0000-0000-0000000000f1");
    public static readonly DateTime Now = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static byte[] Key(byte fill = 7) => Enumerable.Repeat(fill, 32).ToArray();
    public static byte[] Nonce() => Enumerable.Repeat((byte)9, 12).ToArray();
    public static byte[] Ciphertext(int length = 64, byte fill = 5) => Enumerable.Repeat(fill, length).ToArray();

    public static FamilyLink Pending(byte[]? requesterKey = null) =>
        FamilyLink.Request(LinkId, Requester, Recipient, "child", requesterKey, Now).Value!;

    public static FamilyLink Linked(byte[]? requesterKey = null, byte[]? recipientKey = null)
    {
        var link = Pending(requesterKey);
        link.Approve(Recipient, recipientKey, Now.AddMinutes(1));
        return link;
    }
}

/// <summary>A clock that stands still, so handler timestamps are deterministic.</summary>
internal sealed class FixedClock(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utcNow);
}
