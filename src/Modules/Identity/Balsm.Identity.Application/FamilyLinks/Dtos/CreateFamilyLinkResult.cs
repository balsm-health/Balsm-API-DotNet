namespace Balsm.Identity.Application.FamilyLinks.Dtos;

/// <summary>Outcome of a create: the link, and whether this call created it
/// (<c>false</c> = an idempotent replay of an existing id).</summary>
public sealed record CreateFamilyLinkResult(FamilyLinkDto Link, bool Created);
