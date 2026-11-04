using Balsm.Identity.Application.FamilyLinks.Commands;
using Balsm.Identity.Domain.FamilyLinks;
using FluentValidation;

namespace Balsm.Identity.Application.FamilyLinks.Validators;

/// <summary>
/// Envelope shape of a sealed card. The 16 KB ceiling is the aggregate's rule
/// and answers 413, so it is deliberately not repeated here as a 422.
/// </summary>
public sealed class UpsertFamilyLinkCardCommandValidator : AbstractValidator<UpsertFamilyLinkCardCommand>
{
    public UpsertFamilyLinkCardCommandValidator()
    {
        RuleFor(x => x.Version).GreaterThanOrEqualTo(1).WithErrorCode(FamilyLinkErrors.InvalidCard.Code);
        RuleFor(x => x.EphemeralPublicKey)
            .Must(k => k is { Length: FamilyLinkCardShare.PublicKeyBytes })
            .WithErrorCode(FamilyLinkErrors.InvalidCard.Code)
            .WithMessage("The ephemeral public key must be 32 bytes.");
        RuleFor(x => x.Nonce)
            .Must(n => n is { Length: FamilyLinkCardShare.NonceBytes })
            .WithErrorCode(FamilyLinkErrors.InvalidCard.Code)
            .WithMessage("The nonce must be 12 bytes.");
        RuleFor(x => x.Ciphertext)
            .Must(c => c is { Length: > 0 })
            .WithErrorCode(FamilyLinkErrors.InvalidCard.Code)
            .WithMessage("The ciphertext must not be empty.");
    }
}
