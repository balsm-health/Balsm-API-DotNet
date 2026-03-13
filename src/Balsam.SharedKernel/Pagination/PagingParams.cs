namespace Balsam.SharedKernel.Pagination;

public sealed record PagingParams(int PageNumber = 1, int PageSize = 20)
{
    public int PageNumber { get; init; } = PageNumber < 1 ? 1 : PageNumber;
    public int PageSize { get; init; } = PageSize < 1 ? 20 : PageSize > 100 ? 100 : PageSize;
}
