namespace Balsm.Identity.Application.FamilyLinks.Dtos;

/// <summary>The other party of a link — non-PHI names only.</summary>
/// <param name="UserId">The other account's id; present only once the link has been approved.
/// Needed by the app as part of the sealed card's associated data.</param>
/// <param name="Handle">The other account's @handle, if claimed.</param>
/// <param name="DisplayName">The other account's display name, if set.</param>
public sealed record FamilyLinkCounterpartDto(Guid? UserId, string? Handle, string? DisplayName);
