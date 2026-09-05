namespace BuildingBlocks.Pagination;

/// <summary>
/// Paging is a presentation detail, so out-of-range values are clamped rather
/// than rejected. The upper bound matters: without it a client asking for
/// PageSize=1000000 would have that number reach the database directly and pull
/// the whole table.
/// </summary>
public record PaginationRequest
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    public PaginationRequest(int PageIndex = 0, int PageSize = DefaultPageSize)
    {
        this.PageIndex = PageIndex < 0 ? 0 : PageIndex;
        this.PageSize = PageSize switch
        {
            <= 0 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => PageSize
        };
    }

    // Get-only on purpose: an init setter would let an object initializer or a
    // "with" expression put an unclamped value back in.
    public int PageIndex { get; }

    public int PageSize { get; }
}
