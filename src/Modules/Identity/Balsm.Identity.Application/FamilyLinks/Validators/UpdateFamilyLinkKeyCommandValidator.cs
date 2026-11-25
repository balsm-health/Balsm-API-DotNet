using Balsm.Identity.Application.FamilyLinks.Commands;
using Balsm.Identity.Domain.FamilyLinks;
using FluentValidation;

namespace Balsm.Identity.Application.FamilyLinks.Validators;

/// <summary>A key upload must carry a 32-byte X25519 public key.</summary>
public sealed class UpdateFamilyLinkKeyCommandValidator : AbstractValidator<UpdateFamilyLinkKeyCommand>
{
    public UpdateFamilyLinkKeyCommandValidator()
    {
        RuleFor(x => x.PublicKey)
            .Must(FamilyLink.IsValidPublicKey)
            .WithErrorCode(FamilyLinkErrors.InvalidPublicKey.Code)
            .WithMessage(FamilyLinkErrors.InvalidPublicKey.Message);
    }
}
