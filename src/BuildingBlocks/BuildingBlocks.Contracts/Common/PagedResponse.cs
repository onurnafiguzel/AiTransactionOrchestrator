namespace BuildingBlocks.Contracts.Common;

/// <summary>
/// Generic paginated response wrapper
/// </summary>
/// <typeparam name="T">Item type</typeparam>
public sealed record PagedResponse<T>
{
    /// <summary>
    /// Items in current page
    /// </summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>
    /// Current page number
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Page size
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    public int TotalPages { get; init; }

    /// <summary>
    /// Has previous page
    /// </summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Has next page
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Constructor
    /// </summary>
    public PagedResponse(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int totalCount)
    {
        Items = items ?? Array.Empty<T>();
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    /// <summary>
    /// Empty response
    /// </summary>
    public static PagedResponse<T> Empty(int page = 1, int pageSize = 20) =>
        new(Array.Empty<T>(), page, pageSize, 0);
}
