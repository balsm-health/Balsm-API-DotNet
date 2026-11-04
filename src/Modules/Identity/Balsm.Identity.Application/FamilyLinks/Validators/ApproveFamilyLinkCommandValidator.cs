using Balsm.Identity.Application.FamilyLinks.Commands;
using Balsm.Identity.Domain.FamilyLinks;
using FluentValidation;

namespace Balsm.Identity.Application.FamilyLinks.Validators;

/// <summary>An approval may carry the recipient's device key; when it does, it must be a 32-byte X25519 key.</summary>
public sealed class ApproveFamilyLinkCommandValidator : AbstractValidator<ApproveFamilyLinkCommand>
{
    public ApproveFamilyLinkCommandValidator()
    {
        RuleFor(x => x.PublicKey)
            .Must(FamilyLink.IsValidPublicKey)
            .When(x => x.PublicKey is not null)
            .WithErrorCode(FamilyLinkErrors.InvalidPublicKey.Code)
            .WithMessage(FamilyLinkErrors.InvalidPublicKey.Message);
    }
}
