using Balsm.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.Infrastructure.Extensions;

public static class ResultExtensions
{
    public static ActionResult ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return new OkObjectResult(result.Value);

        var code = result.Error?.Code ?? string.Empty;

        if (code == "NotFound" || code.EndsWith(".NotFound", StringComparison.Ordinal))
            return new NotFoundObjectResult(result.Error);

        if (code == "Conflict" || code.EndsWith(".AlreadyExists", StringComparison.Ordinal))
            return new ConflictObjectResult(result.Error);

        if (code == "ValidationFailed" || code.EndsWith(".Validation", StringComparison.Ordinal))
            return new UnprocessableEntityObjectResult(result.Error);

        if (code == "Unauthorized")
            return new UnauthorizedObjectResult(result.Error);

        if (code == "Forbidden")
            return new ObjectResult(result.Error) { StatusCode = 403 };

        return new BadRequestObjectResult(result.Error);
    }
}
