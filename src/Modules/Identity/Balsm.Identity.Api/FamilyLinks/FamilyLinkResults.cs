using System.Text.Json;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.SharedKernel.Results;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.Identity.Api.FamilyLinks;

/// <summary>Translates family-link failures into the wire contract (status code + error envelope).</summary>
internal static class FamilyLinkResults
{
    private static readonly Dictionary<string, int> StatusByCode = new(StringComparer.Ordinal)
    {
        [FamilyLinkErrors.NotFound.Code] = StatusCodes.Status404NotFound,
        [FamilyLinkErrors.SelfLink.Code] = StatusCodes.Status409Conflict,
        [FamilyLinkErrors.AlreadyExists.Code] = StatusCodes.Status409Conflict,
        [FamilyLinkErrors.NotPending.Code] = StatusCodes.Status409Conflict,
        [FamilyLinkErrors.NotLinked.Code] = StatusCodes.Status409Conflict,
        [FamilyLinkErrors.RecipientMustDecline.Code] = StatusCodes.Status409Conflict,
        [FamilyLinkErrors.Ended.Code] = StatusCodes.Status409Conflict,
        [FamilyLinkErrors.ConcurrentUpdate.Code] = StatusCodes.Status409Conflict,
        [FamilyLinkErrors.StaleCardVersion.Code] = StatusCodes.Status409Conflict,
        [FamilyLinkErrors.CardTooLarge.Code] = StatusCodes.Status413PayloadTooLarge,
        [FamilyLinkErrors.InvalidRelation.Code] = StatusCodes.Status422UnprocessableEntity,
        [FamilyLinkErrors.InvalidId.Code] = StatusCodes.Status422UnprocessableEntity,
        [FamilyLinkErrors.InvalidPublicKey.Code] = StatusCodes.Status422UnprocessableEntity,
        [FamilyLinkErrors.InvalidCard.Code] = StatusCodes.Status422UnprocessableEntity,
    };

    public static ObjectResult Failure(Error error, string? correlationId) => new(
        new FamilyLinkErrorResponse(new FamilyLinkError(error.Code, error.Message, correlationId)))
    {
        StatusCode = StatusByCode.GetValueOrDefault(error.Code, StatusCodes.Status400BadRequest),
    };

    /// <summary>All validation failures at once, field names in the wire's snake_case.</summary>
    public static ObjectResult Invalid(ValidationResult validation, string? correlationId) => new(
        new FamilyLinkErrorResponse(new FamilyLinkError(
            Error.ValidationFailed.Code,
            Error.ValidationFailed.Message,
            correlationId,
            validation.Errors
                .Select(e => new FamilyLinkFieldError(
                    JsonNamingPolicy.SnakeCaseLower.ConvertName(e.PropertyName), e.ErrorCode, e.ErrorMessage))
                .ToList())))
    {
        StatusCode = StatusCodes.Status422UnprocessableEntity,
    };
}
