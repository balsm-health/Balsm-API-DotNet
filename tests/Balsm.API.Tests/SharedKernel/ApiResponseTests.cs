using Balsm.SharedKernel.Api;
using Balsm.SharedKernel.Results;
using FluentAssertions;
using Xunit;

namespace Balsm.API.Tests.SharedKernel;

public sealed class ApiResponseTests
{
    [Fact]
    public void Ok_CarriesDataAndNoError()
    {
        var response = ApiResponse<int>.Ok(42);

        response.Should().BeEquivalentTo(new { Data = 42, Error = (ApiError?)null });
    }

    [Fact]
    public void Fail_CarriesErrorAndNoData()
    {
        var error = new ApiError("Code", "Message", "corr-1");

        var response = ApiResponse<string>.Fail(error);

        response.Should().BeEquivalentTo(new { Data = (string?)null, Error = error });
    }

    [Fact]
    public void FromResult_Success_WrapsTheValue()
    {
        var response = ApiResponse<string>.FromResult(Result.Success("value"), "corr-2");

        response.Data.Should().Be("value");
    }

    [Fact]
    public void FromResult_Failure_MapsTheErrorWithTheCorrelationId()
    {
        var response = ApiResponse<string>.FromResult(Result.Failure<string>(Error.NotFound), "corr-3");

        response.Error.Should().Be(new ApiError("NotFound", Error.NotFound.Message, "corr-3"));
    }
}
