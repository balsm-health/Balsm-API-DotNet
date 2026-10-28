using Balsm.SharedKernel.Pagination;
using FluentAssertions;
using Xunit;

namespace Balsm.API.Tests.SharedKernel;

public sealed class PagedListTests
{
    [Fact]
    public void Ctor_StoresWhatItWasGiven()
    {
        var list = new PagedList<string>(["a", "b"], totalCount: 12, pageNumber: 2, pageSize: 5);

        list.Should().BeEquivalentTo(new { Items = new[] { "a", "b" }, TotalCount = 12, PageNumber = 2, PageSize = 5 });
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(25, 5, 5)]
    public void TotalPages_RoundsUp(int totalCount, int pageSize, int expected)
    {
        var list = new PagedList<int>([], totalCount, 1, pageSize);

        list.TotalPages.Should().Be(expected);
    }

    [Fact]
    public void HasPreviousPage_OnFirstPage_IsFalse()
    {
        var list = new PagedList<int>([], 30, 1, 10);

        list.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void HasPreviousPage_AfterFirstPage_IsTrue()
    {
        var list = new PagedList<int>([], 30, 2, 10);

        list.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public void HasNextPage_BeforeLastPage_IsTrue()
    {
        var list = new PagedList<int>([], 30, 2, 10);

        list.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void HasNextPage_OnLastPage_IsFalse()
    {
        var list = new PagedList<int>([], 30, 3, 10);

        list.HasNextPage.Should().BeFalse();
    }
}
