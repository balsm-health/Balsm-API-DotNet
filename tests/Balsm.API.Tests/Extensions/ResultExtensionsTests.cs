using Balsm.Infrastructure.Extensions;
using Balsm.SharedKernel.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Balsm.API.Tests.Extensions;

public sealed class ResultExtensionsTests
{
    [Fact]
    public void ToActionResult_Success_Returns200WithTheValue()
    {
        var result = Result.Success("value").ToActionResult();

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be("value");
    }

    [Theory]
    [InlineData("NotFound", 404)]
    [InlineData("Account.NotFound", 404)]
    [InlineData("Conflict", 409)]
    [InlineData("Handle.AlreadyExists", 409)]
    [InlineData("ValidationFailed", 422)]
    [InlineData("Dob.Validation", 422)]
    [InlineData("Unauthorized", 401)]
    [InlineData("Forbidden", 403)]
    [InlineData("Something.Else", 400)]
    public void ToActionResult_Failure_MapsTheErrorCodeToAStatus(string code, int expected)
    {
        var result = Result.Failure<string>(new Error(code, "message")).ToActionResult();

        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(expected);
    }

    [Fact]
    public void ToActionResult_FailureWithoutError_Returns400()
    {
        var result = Result.Failure<string>(null!).ToActionResult();

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
