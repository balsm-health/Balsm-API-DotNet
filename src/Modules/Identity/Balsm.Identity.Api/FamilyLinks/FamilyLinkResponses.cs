using Balsm.Identity.Application.FamilyLinks.Dtos;

namespace Balsm.Identity.Api.FamilyLinks;

/// <summary><c>{ "data": FamilyLinkDto }</c></summary>
public sealed record FamilyLinkResponse(FamilyLinkDto Data);

/// <summary><c>{ "data": { items, page, page_size, total_count, has_next_page } }</c></summary>
public sealed record FamilyLinkListResponse(FamilyLinkPageDto Data);

/// <summary>One page of links, newest first.</summary>
public sealed record FamilyLinkPageDto(
    IReadOnlyList<FamilyLinkDto> Items, int Page, int PageSize, int TotalCount, bool HasNextPage);

/// <summary><c>{ "data": FamilyLinkCardDto }</c></summary>
public sealed record FamilyLinkCardResponse(FamilyLinkCardDto Data);

/// <summary><c>{ "error": { code, message, correlation_id, fields } }</c> — the envelope every failure uses.</summary>
public sealed record FamilyLinkErrorResponse(FamilyLinkError Error);

/// <summary>A failure. <c>code</c> is machine-readable (e.g. <c>FamilyLink.NotFound</c>).</summary>
/// <param name="Fields">Per-field validation failures; present only on a 422 <c>ValidationFailed</c>.</param>
public sealed record FamilyLinkError(
    string Code, string Message, string? CorrelationId, IReadOnlyList<FamilyLinkFieldError>? Fields = null);

/// <summary>One validation failure: snake_case field name, machine-readable code, human-readable message.</summary>
public sealed record FamilyLinkFieldError(string Field, string Code, string Message);
