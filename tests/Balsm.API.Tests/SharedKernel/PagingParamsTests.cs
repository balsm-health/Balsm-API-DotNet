using Balsm.SharedKernel.Pagination;
using FluentAssertions;
using Xunit;

namespace Balsm.API.Tests.SharedKernel;

public sealed class PagingParamsTests
{
    [Fact]
    public void Ctor_NoArguments_DefaultsToFirstPageOfTwenty()
    {
        var paging = new PagingParams();

        paging.Should().Be(new PagingParams(1, 20));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Ctor_PageNumberBelowOne_ClampsToOne(int pageNumber)
    {
        var paging = new PagingParams(pageNumber);

        paging.PageNumber.Should().Be(1);
    }

    [Fact]
    public void Ctor_PageNumberAboveOne_KeepsIt()
    {
        var paging = new PagingParams(7);

        paging.PageNumber.Should().Be(7);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(101, 100)]
    [InlineData(100, 100)]
    [InlineData(1, 1)]
    [InlineData(35, 35)]
    public void Ctor_PageSize_IsClampedToOneThroughOneHundred(int requested, int expected)
    {
        var paging = new PagingParams(1, requested);

        paging.PageSize.Should().Be(expected);
    }
}
