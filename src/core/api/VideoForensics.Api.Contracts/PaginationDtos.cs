namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Offset-based pagination result data transfer object for large datasets.
    /// </summary>
    /// <typeparam name="T">The type of items in the paginated result.</typeparam>
    /// <param name="Items">The items for the current page.</param>
    /// <param name="TotalCount">Total count of items across all pages.</param>
    /// <param name="PageNumber">The current page number (1-based).</param>
    /// <param name="PageSize">The maximum number of items per page.</param>
    public record PaginatedResultDto<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize
    );

    /// <summary>
    /// Cursor-based pagination result data transfer object for streamable and live data.
    /// </summary>
    /// <typeparam name="T">The type of items in the cursor-paginated result.</typeparam>
    /// <param name="Items">The items in the current result set.</param>
    /// <param name="NextCursor">Opaque cursor token for fetching the next page, or null if at the end of the result set.</param>
    /// <param name="HasMore">True if additional pages are available; False if this is the final page.</param>
    public record CursorPaginatedResultDto<T>(
        IReadOnlyList<T> Items,
        string? NextCursor,
        bool HasMore
    );
}
