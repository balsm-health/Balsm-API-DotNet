using Balsm.API.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Balsm.API.Controllers;

/// <summary>
/// Platform-owned, patient-facing reference content. General information, the
/// same for every caller — never patient data.
/// </summary>
[ApiController]
[Route("api/v1/content")]
[Authorize]
[Produces("application/json")]
[Tags("Platform/Content")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class ContentController : ControllerBase
{
    /// <summary>Lets a client keep the document for a day before revalidating with If-None-Match.</summary>
    internal const string CacheControl = "private, max-age=86400";

    /// <summary>Week-by-week pregnancy guidance (weeks 4–40) in the requested language.</summary>
    /// <remarks>
    /// Requires an authenticated patient (any valid access token; no further permission —
    /// the content is general and identical for everyone). Served from a static file of
    /// sourced copy. <c>lang</c> is <c>en</c> or <c>ar</c>; anything else, or none, gets
    /// English. Each language has its own <c>ETag</c>: send the last one as
    /// <c>If-None-Match</c> with the same <c>lang</c> to get 304 when nothing changed.
    /// </remarks>
    /// <param name="lang">Language of every text in the response: <c>en</c> (default) or <c>ar</c>.</param>
    [HttpGet("pregnancy-weeks")]
    [EndpointName("Platform_GetPregnancyWeeks")]
    [EndpointSummary("Get week-by-week pregnancy guidance")]
    [EndpointDescription("Returns general, non-personal guidance for each week of pregnancy from week 4 to 40, with its sizes, tests, danger signs, topics, sources, artwork credits and the guide's own wording, in the language `lang` asks for (`en` or `ar`; anything else gets English). Conditional: honours If-None-Match per language and answers 304 when the caller's copy is current.")]
    [ProducesResponseType<PregnancyWeeksResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public IActionResult PregnancyWeeks([FromQuery] string? lang)
    {
        var content = PregnancyWeeksContent.Current.For(lang);
        var headers = Response.GetTypedHeaders();
        headers.ETag = content.ETag;
        Response.Headers.CacheControl = CacheControl;

        var ifNoneMatch = Request.GetTypedHeaders().IfNoneMatch;
        if (ifNoneMatch.Any(tag => tag.Equals(EntityTagHeaderValue.Any) || tag.Compare(content.ETag, useStrongComparison: false)))
            return StatusCode(StatusCodes.Status304NotModified);

        return File(content.Body, "application/json");
    }
}
