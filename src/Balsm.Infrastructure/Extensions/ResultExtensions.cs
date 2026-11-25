using Balsm.SharedKernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.Infrastructure.Extensions;

public static class ResultExtensions
{
    public static ActionResult ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return new OkObjectResult(result.Value);

        return new ObjectResult(result.Error) { StatusCode = GetStatusCode(result.Error) };
    }

    public static IActionResult ToProblem(this Result result, HttpContext http)
    {
        var status = GetStatusCode(result.Error!);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = result.Error!.Message,
            Type = $"https://httpstatuses.io/{status}"
        };
        problem.Extensions["correlationId"] = http.Items["CorrelationId"]?.ToString();

        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }

    private static int GetStatusCode(Error? error)
    {
        var code = error?.Code ?? string.Empty;

        if (code == "NotFound" || code.EndsWith(".NotFound", StringComparison.Ordinal))
            return StatusCodes.Status404NotFound;
        if (code == "Conflict" || code.EndsWith(".AlreadyExists", StringComparison.Ordinal))
            return StatusCodes.Status409Conflict;
        if (code == "ValidationFailed" || code.EndsWith(".Validation", StringComparison.Ordinal))
            return StatusCodes.Status422UnprocessableEntity;
        if (code == "Unauthorized")
            return StatusCodes.Status401Unauthorized;
        if (code == "Forbidden")
            return StatusCodes.Status403Forbidden;

        return StatusCodes.Status400BadRequest;
    }
}