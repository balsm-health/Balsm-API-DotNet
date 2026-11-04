using System.Security.Claims;
using Balsm.Identity.Api.FamilyLinks;
using Balsm.Identity.Application.FamilyLinks.Commands;
using Balsm.Identity.Application.FamilyLinks.Queries;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace Balsm.Identity.Api.Controllers;

/// <summary>
/// Family links between two Balsm accounts: the requester scans the other
/// person's profile QR, the recipient approves in their own app (by pulling —
/// there is no push channel), and either side may then share an end-to-end
/// sealed emergency card. Who acts is always the token's sub claim; anyone who
/// is not a party to a link gets 404 on every route (threat E10).
/// </summary>
[ApiController]
[Route("api/v1/family-links")]
[Authorize]
[Produces("application/json")]
[Tags("Identity/FamilyLinks")]
[ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status401Unauthorized)]
public sealed class FamilyLinksController(IMediator mediator) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? Guid.Empty.ToString());

    private string? CorrelationId => HttpContext.Items["CorrelationId"] as string ?? HttpContext.TraceIdentifier;

    /// <summary>Requests a link with the owner of a scanned profile QR.</summary>
    /// <remarks>Requires an authenticated patient. Idempotent on the client-minted id: a replay answers 200 with the
    /// existing link. Unknown, revoked and expired jtis all answer the same 404. Rate-limited per account.</remarks>
    [HttpPost]
    [Consumes("application/json")]
    [FamilyLinkCreateRateLimit]
    [EndpointName("Identity_CreateFamilyLink")]
    [EndpointSummary("Request a family link by profile-QR jti")]
    [EndpointDescription("Creates a pending link from the caller to the account that owns the scanned profile QR. The jti is resolved once; the link then binds to the two account ids.")]
    [ProducesResponseType<FamilyLinkResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<FamilyLinkResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Create(
        [FromBody] CreateFamilyLinkRequest req,
        [FromServices] IValidator<CreateFamilyLinkCommand> validator,
        CancellationToken ct)
    {
        var cmd = new CreateFamilyLinkCommand(req.Id, CurrentUserId, req.Jti, req.Relation, req.PublicKey, CorrelationId);
        var validation = await validator.ValidateAsync(cmd, ct);
        if (!validation.IsValid) return FamilyLinkResults.Invalid(validation, CorrelationId);

        var result = await mediator.Send(cmd, ct);
        if (result.IsFailure) return FamilyLinkResults.Failure(result.Error!, CorrelationId);

        var body = new FamilyLinkResponse(result.Value!.Link);
        return result.Value.Created
            ? CreatedAtAction(nameof(Get), new { id = body.Data.Id }, body)
            : Ok(body);
    }

    /// <summary>Links addressed to the caller (they are the recipient).</summary>
    /// <remarks>Requires an authenticated patient. The app pulls this on start, resume and the Family screen.</remarks>
    [HttpGet("incoming")]
    [EndpointName("Identity_ListIncomingFamilyLinks")]
    [EndpointSummary("List incoming family-link requests")]
    [EndpointDescription("Newest first, paged. Filter with ?status=pending for open requests. Each item shows the requester's @handle and display name.")]
    [ProducesResponseType<FamilyLinkListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> ListIncoming(
        [FromQuery] string? status,
        [FromServices] IValidator<ListIncomingFamilyLinksQuery> validator,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 20) =>
        ListAsync(new ListIncomingFamilyLinksQuery(CurrentUserId, status, page, pageSize), validator, ct);

    /// <summary>Links the caller asked for (they are the requester).</summary>
    /// <remarks>Requires an authenticated patient. The recipient's names appear only after they approve.</remarks>
    [HttpGet("outgoing")]
    [EndpointName("Identity_ListOutgoingFamilyLinks")]
    [EndpointSummary("List outgoing family-link requests and links")]
    [EndpointDescription("Newest first, paged, optional ?status filter. Shows pending → linked without revealing the recipient before approval.")]
    [ProducesResponseType<FamilyLinkListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> ListOutgoing(
        [FromQuery] string? status,
        [FromServices] IValidator<ListOutgoingFamilyLinksQuery> validator,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 20) =>
        ListAsync(new ListOutgoingFamilyLinksQuery(CurrentUserId, status, page, pageSize), validator, ct);

    /// <summary>One link, including the other side's device public key while linked.</summary>
    /// <remarks>Requires an authenticated patient who is a party to the link; anyone else gets 404.</remarks>
    [HttpGet("{id:guid}")]
    [EndpointName("Identity_GetFamilyLink")]
    [EndpointSummary("Get one family link")]
    [ProducesResponseType<FamilyLinkResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetFamilyLinkByIdQuery(id, CurrentUserId), ct);
        return result.IsSuccess
            ? Ok(new FamilyLinkResponse(result.Value!))
            : FamilyLinkResults.Failure(result.Error!, CorrelationId);
    }

    /// <summary>The recipient approves a pending request.</summary>
    /// <remarks>Requires an authenticated patient who is the recipient; anyone else gets 404. The body is optional.</remarks>
    [HttpPost("{id:guid}/approve")]
    [EndpointName("Identity_ApproveFamilyLink")]
    [EndpointSummary("Approve a family-link request")]
    [EndpointDescription("pending → linked. Records the recipient's consent and, if sent, their device public key.")]
    [ProducesResponseType<FamilyLinkResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ApproveFamilyLinkRequest? req,
        [FromServices] IValidator<ApproveFamilyLinkCommand> validator,
        CancellationToken ct)
    {
        var cmd = new ApproveFamilyLinkCommand(id, CurrentUserId, req?.PublicKey, CorrelationId);
        var validation = await validator.ValidateAsync(cmd, ct);
        if (!validation.IsValid) return FamilyLinkResults.Invalid(validation, CorrelationId);

        var result = await mediator.Send(cmd, ct);
        return result.IsSuccess
            ? Ok(new FamilyLinkResponse(result.Value!))
            : FamilyLinkResults.Failure(result.Error!, CorrelationId);
    }

    /// <summary>The recipient declines a pending request.</summary>
    /// <remarks>Requires an authenticated patient who is the recipient; anyone else gets 404.</remarks>
    [HttpPost("{id:guid}/decline")]
    [EndpointName("Identity_DeclineFamilyLink")]
    [EndpointSummary("Decline a family-link request")]
    [ProducesResponseType<FamilyLinkResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Decline(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeclineFamilyLinkCommand(id, CurrentUserId, CorrelationId), ct);
        return result.IsSuccess
            ? Ok(new FamilyLinkResponse(result.Value!))
            : FamilyLinkResults.Failure(result.Error!, CorrelationId);
    }

    /// <summary>Cancels a pending request (requester) or unlinks an active link (either side).</summary>
    /// <remarks>Requires an authenticated party to the link. Unlinking purges both sealed cards. Never a hard
    /// delete; idempotent once ended. The recipient of a pending request declines it instead (409).</remarks>
    [HttpDelete("{id:guid}")]
    [EndpointName("Identity_DeleteFamilyLink")]
    [EndpointSummary("Cancel or unlink a family link")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        NoContentOr(await mediator.Send(new DeleteFamilyLinkCommand(id, CurrentUserId, CorrelationId), ct));

    /// <summary>Uploads the caller's current device X25519 public key for this link.</summary>
    /// <remarks>Requires an authenticated party to the link. Used after a phone change so the other side can re-seal.</remarks>
    [HttpPut("{id:guid}/keys")]
    [Consumes("application/json")]
    [EndpointName("Identity_UpdateFamilyLinkKey")]
    [EndpointSummary("Upload this device's public key for a link")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateKey(
        Guid id,
        [FromBody] UpdateFamilyLinkKeyRequest req,
        [FromServices] IValidator<UpdateFamilyLinkKeyCommand> validator,
        CancellationToken ct)
    {
        var cmd = new UpdateFamilyLinkKeyCommand(id, CurrentUserId, req.PublicKey, CorrelationId);
        var validation = await validator.ValidateAsync(cmd, ct);
        if (!validation.IsValid) return FamilyLinkResults.Invalid(validation, CorrelationId);
        return NoContentOr(await mediator.Send(cmd, ct));
    }

    /// <summary>Stores a newer version of the caller's emergency card, sealed for the other party.</summary>
    /// <remarks>Requires an authenticated party to a linked link (the card's owner). Opt-in, off by default. Only
    /// newer versions are accepted; an identical replay answers 204. Ciphertext over 16 KB answers 413.</remarks>
    [HttpPut("{id:guid}/card")]
    [Consumes("application/json")]
    [RequestSizeLimit(64 * 1024)]
    [EndpointName("Identity_UpsertFamilyLinkCard")]
    [EndpointSummary("Share (or refresh) my sealed emergency card on a link")]
    [EndpointDescription("ECIES: X25519 + HKDF-SHA256 (info balsm-family-card-v1, salt = link id) + AES-256-GCM, AAD = link id | owner user id | version. The server stores the envelope only.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertCard(
        Guid id,
        [FromBody] UpsertFamilyLinkCardRequest req,
        [FromServices] IValidator<UpsertFamilyLinkCardCommand> validator,
        CancellationToken ct)
    {
        var cmd = new UpsertFamilyLinkCardCommand(
            id, CurrentUserId, req.Version, req.EphemeralPublicKey, req.Nonce, req.Ciphertext, CorrelationId);
        var validation = await validator.ValidateAsync(cmd, ct);
        if (!validation.IsValid) return FamilyLinkResults.Invalid(validation, CorrelationId);
        return NoContentOr(await mediator.Send(cmd, ct));
    }

    /// <summary>The newest card the other party shares with the caller.</summary>
    /// <remarks>Requires an authenticated party to a linked link. 404 when nothing is shared. Every read is audited.</remarks>
    [HttpGet("{id:guid}/card")]
    [EndpointName("Identity_GetFamilyLinkCard")]
    [EndpointSummary("Get the sealed emergency card shared with me")]
    [ProducesResponseType<FamilyLinkCardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCard(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetFamilyLinkCardQuery(id, CurrentUserId, CorrelationId), ct);
        if (result.IsFailure) return FamilyLinkResults.Failure(result.Error!, CorrelationId);

        Response.Headers.CacheControl = "no-store";
        return Ok(new FamilyLinkCardResponse(result.Value!));
    }

    /// <summary>Stops sharing the caller's card on this link; the ciphertext is purged.</summary>
    /// <remarks>Requires an authenticated party to the link. Idempotent.</remarks>
    [HttpDelete("{id:guid}/card")]
    [EndpointName("Identity_DeleteFamilyLinkCard")]
    [EndpointSummary("Stop sharing my emergency card on a link")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<FamilyLinkErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCard(Guid id, CancellationToken ct) =>
        NoContentOr(await mediator.Send(new DeleteFamilyLinkCardCommand(id, CurrentUserId, CorrelationId), ct));

    private async Task<IActionResult> ListAsync<TQuery>(TQuery query, IValidator<TQuery> validator, CancellationToken ct)
        where TQuery : IRequest<SharedKernel.Results.Result<SharedKernel.Pagination.PagedList<Application.FamilyLinks.Dtos.FamilyLinkDto>>>
    {
        var validation = await validator.ValidateAsync(query, ct);
        if (!validation.IsValid) return FamilyLinkResults.Invalid(validation, CorrelationId);

        var page = (await mediator.Send(query, ct)).Value!;
        return Ok(new FamilyLinkListResponse(new FamilyLinkPageDto(
            page.Items, page.PageNumber, page.PageSize, page.TotalCount, page.HasNextPage)));
    }

    private IActionResult NoContentOr(SharedKernel.Results.Result result) =>
        result.IsSuccess ? NoContent() : FamilyLinkResults.Failure(result.Error!, CorrelationId);
}
