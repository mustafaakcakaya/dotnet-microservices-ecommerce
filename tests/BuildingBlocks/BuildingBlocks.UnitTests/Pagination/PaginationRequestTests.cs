using BuildingBlocks.Pagination;

namespace BuildingBlocks.UnitTests.Pagination;

/// <summary>
/// PageSize used to be unbounded, so a client asking for a million rows had that
/// number reach the database untouched. Out-of-range values are clamped rather
/// than rejected: paging is a presentation detail, not a reason to fail a request.
/// </summary>
public sealed class PaginationRequestTests
{
    [Fact]
    public void Defaults_AreFirstPageAndDefaultSize()
    {
        var request = new PaginationRequest();

        Assert.Equal(0, request.PageIndex);
        Assert.Equal(PaginationRequest.DefaultPageSize, request.PageSize);
    }

    [Theory]
    [InlineData(1_000_000)]
    [InlineData(PaginationRequest.MaxPageSize + 1)]
    [InlineData(int.MaxValue)]
    public void PageSize_AboveMaximum_IsClampedToMaximum(int requested)
    {
        var request = new PaginationRequest(PageIndex: 0, PageSize: requested);

        Assert.Equal(PaginationRequest.MaxPageSize, request.PageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void PageSize_NotPositive_FallsBackToDefault(int requested)
    {
        var request = new PaginationRequest(PageIndex: 0, PageSize: requested);

        Assert.Equal(PaginationRequest.DefaultPageSize, request.PageSize);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void PageIndex_Negative_IsClampedToFirstPage(int requested)
    {
        var request = new PaginationRequest(PageIndex: requested, PageSize: 10);

        Assert.Equal(0, request.PageIndex);
    }

    [Fact]
    public void ValuesWithinRange_ArePreserved()
    {
        var request = new PaginationRequest(PageIndex: 3, PageSize: 25);

        Assert.Equal(3, request.PageIndex);
        Assert.Equal(25, request.PageSize);
    }

    [Fact]
    public void MaximumItself_IsAllowed()
    {
        var request = new PaginationRequest(PageIndex: 0, PageSize: PaginationRequest.MaxPageSize);

        Assert.Equal(PaginationRequest.MaxPageSize, request.PageSize);
    }
}
