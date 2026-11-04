using Balsm.Identity.Application.FamilyLinks.Commands;
using Balsm.Identity.Domain.FamilyLinks;
using FluentValidation;

namespace Balsm.Identity.Application.FamilyLinks.Validators;

/// <summary>Shape checks for a link request; the aggregate re-checks the rules.</summary>
public sealed class CreateFamilyLinkCommandValidator : AbstractValidator<CreateFamilyLinkCommand>
{
    public CreateFamilyLinkCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithErrorCode(FamilyLinkErrors.InvalidId.Code);
        RuleFor(x => x.Jti).NotEmpty().WithErrorCode("FamilyLink.InvalidJti").WithMessage("A profile-QR jti is required.");
        RuleFor(x => x.Relation)
            .Must(FamilyRelation.IsAllowed)
            .WithErrorCode(FamilyLinkErrors.InvalidRelation.Code)
            .WithMessage($"Relation must be one of: {string.Join(", ", FamilyRelation.Allowed.Order())}.");
        RuleFor(x => x.PublicKey)
            .Must(FamilyLink.IsValidPublicKey)
            .When(x => x.PublicKey is not null)
            .WithErrorCode(FamilyLinkErrors.InvalidPublicKey.Code)
            .WithMessage(FamilyLinkErrors.InvalidPublicKey.Message);
    }
}
