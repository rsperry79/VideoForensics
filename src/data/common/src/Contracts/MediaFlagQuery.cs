using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Filter for flag lists, counts and neighbour lookups. Null or empty collections mean "any".</summary>
    public sealed record MediaFlagFilter
    {
        public IReadOnlyCollection<MediaFlagType>? Types { get; init; }
        public IReadOnlyCollection<MediaFlagStatus>? Statuses { get; init; }
        public IReadOnlyCollection<Guid>? DeviceIds { get; init; }

        /// <summary>Inclusive lower bound on the flag window start.</summary>
        public DateTime? FromUtc { get; init; }

        /// <summary>Inclusive upper bound on the flag window start.</summary>
        public DateTime? ToUtc { get; init; }
    }

    /// <summary>One page of flags plus the total number matching the filter.</summary>
    public sealed record MediaFlagPage(IReadOnlyList<MediaFlag> Items, int TotalCount);

    /// <summary>Direction in the list order (newest raised first): Next is the following, older flag.</summary>
    public enum MediaFlagNeighbour
    {
        Previous,
        Next
    }
}
