using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// Persists the video search, location event, location history, doorbot history and app location history feeds.
    /// Each event method replaces the stored row for its key and every child row of that event in one transaction:
    /// the previous event and its children are deleted, then the new graph is inserted. Children are never merged.
    /// </summary>
    public interface IRingEventHistoryWriter
    {
        /// <summary>Replaces a video search result, keyed by Ring account and <see cref="RingVideoSearchEvent.ProviderEventId"/>.</summary>
        Task ReplaceVideoSearchEventAsync(Guid ringAccountId, RingVideoSearchEvent evt, RingEventDetails details, CancellationToken ct);

        /// <summary>Replaces the location-events paging cursor for a location (keyed by <see cref="RingLocationEventPage.ProviderLocationId"/>).</summary>
        Task ReplaceLocationEventPageAsync(RingLocationEventPage page, CancellationToken ct);

        /// <summary>Replaces a location event, keyed by <see cref="RingLocationEvent.ProviderEventId"/>.</summary>
        Task ReplaceLocationEventAsync(RingLocationEvent evt, RingEventDetails details, CancellationToken ct);

        /// <summary>Replaces the evm location history paging cursor for a location (keyed by <see cref="RingLocationHistoryPage.ProviderLocationId"/>).</summary>
        Task ReplaceLocationHistoryPageAsync(RingLocationHistoryPage page, CancellationToken ct);

        /// <summary>Replaces an evm location history entry, keyed by <see cref="RingLocationHistoryEvent.ProviderEventId"/>.</summary>
        Task ReplaceLocationHistoryEventAsync(RingLocationHistoryEvent evt, RingEventDetails details, CancellationToken ct);

        /// <summary>Replaces a doorbot history event, keyed by <see cref="RingDoorbotHistoryEvent.ProviderEventId"/>.</summary>
        Task ReplaceDoorbotHistoryEventAsync(RingDoorbotHistoryEvent evt, RingEventDetails details, CancellationToken ct);

        /// <summary>Replaces an app location history message, keyed by <see cref="RingAppLocationHistoryMessage.EventId"/>.</summary>
        Task ReplaceAppLocationHistoryMessageAsync(RingAppLocationHistoryMessage message, CancellationToken ct);
    }
}
