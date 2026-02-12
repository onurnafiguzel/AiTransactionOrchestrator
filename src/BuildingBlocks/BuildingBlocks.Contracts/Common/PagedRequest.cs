namespace BuildingBlocks.Contracts.Common;

/// <summary>
/// Base request for paginated queries
/// </summary>
public sealed record PagedRequest
{
    /// <summary>
    /// Page number (starts from 1)
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Page size (number of items per page)
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// Sort field (optional)
    /// </summary>
    public string? SortBy { get; init; }

    /// <summary>
    /// Sort direction: asc or desc
    /// </summary>
    public string SortDirection { get; init; } = "desc";

    /// <summary>
    /// Calculate skip count for database queries
    /// </summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// Validate and normalize page request
    /// </summary>
    public PagedRequest Normalize()
    {
        var normalizedPage = Math.Max(1, Page);
        var normalizedPageSize = Math.Clamp(PageSize, 1, 100);
        var normalizedSortDirection = SortDirection?.ToLower() == "asc" ? "asc" : "desc";

        return this with
        {
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            SortDirection = normalizedSortDirection
        };
    }
}
