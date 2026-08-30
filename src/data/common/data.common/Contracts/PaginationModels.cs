namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Offset-based pagination result for large datasets.</summary>
    public class PaginatedResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize <= 0 ? 0 : (TotalCount + PageSize - 1) / PageSize;
        public bool HasNextPage => PageNumber < TotalPages;
        public bool HasPreviousPage => PageNumber > 1;
    }

    /// <summary>Cursor-based pagination result for streamable/live data.</summary>
    public class CursorPaginatedResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = new List<T>();
        public string? NextCursor { get; set; }
        public bool HasMore { get; set; }
        public int Count => Items.Count;
    }

    /// <summary>Lightweight summary for quick decisions, with detail-on-demand link.</summary>
    public class QuerySummary
    {
        public int TotalCount { get; set; }
        public string Status { get; set; } = string.Empty; // "Healthy", "Anomalies", "Critical"
        public double? ComplianceScore { get; set; } // 0-100 for integrity/health metrics
        public Dictionary<string, int> TopIssues { get; set; } = new(); // Issue type → count
        public DateTime AnalyzedAtUtc { get; set; } = DateTime.UtcNow;
        public string DetailQueryMethod { get; set; } = string.Empty; // Which method to call for full data
    }

    /// <summary>Streaming chunk wrapper for large report resources.</summary>
    public class StreamChunk
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public int ChunkIndex { get; set; }
        public bool IsLastChunk { get; set; }
        public int TotalChunks { get; set; }
    }
}
