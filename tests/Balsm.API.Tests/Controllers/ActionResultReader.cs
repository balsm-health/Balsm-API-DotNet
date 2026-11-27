using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.API.Tests.Controllers;

/// <summary>
/// Reads the anonymous payloads the host controllers return. Round-tripping
/// through JSON keeps the assertions on the wire shape rather than on
/// compiler-generated anonymous types.
/// </summary>
internal static class ActionResultReader
{
    public static JsonElement Body<TResult>(IActionResult result) where TResult : ObjectResult
    {
        var value = result.Should().BeOfType<TResult>().Subject.Value;
        return JsonSerializer.SerializeToElement(value);
    }

    public static JsonElement Ok(IActionResult result) => Body<OkObjectResult>(result);
}
