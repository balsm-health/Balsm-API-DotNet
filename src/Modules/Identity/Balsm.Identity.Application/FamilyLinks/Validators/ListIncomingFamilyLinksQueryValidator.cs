using Balsm.Identity.Application.FamilyLinks.Queries;
using FluentValidation;

namespace Balsm.Identity.Application.FamilyLinks.Validators;

/// <summary>Paging bounds and a known status filter.</summary>
public sealed class ListIncomingFamilyLinksQueryValidator : AbstractValidator<ListIncomingFamilyLinksQuery>
{
    public ListIncomingFamilyLinksQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithErrorCode("FamilyLink.InvalidPage");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithErrorCode("FamilyLink.InvalidPageSize");
        RuleFor(x => x.Status)
            .Must(s => FamilyLinkWire.TryParseStatus(s, out _))
            .When(x => x.Status is not null)
            .WithErrorCode("FamilyLink.InvalidStatus")
            .WithMessage("Status must be one of: pending, linked, declined, cancelled, unlinked.");
    }
}
