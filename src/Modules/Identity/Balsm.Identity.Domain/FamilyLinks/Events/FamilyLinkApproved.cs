using Balsm.SharedKernel.Events;

namespace Balsm.Identity.Domain.FamilyLinks.Events;

/// <summary>A pending family link was approved by its recipient; the two accounts are now linked.</summary>
public sealed record FamilyLinkApproved(Guid FamilyLinkId, Guid RequesterUserId, Guid RecipientUserId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
