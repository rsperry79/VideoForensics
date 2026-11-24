using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>Which source a review Events tab row came from.</summary>
public enum ReviewEventRowKind
{
    /// <summary>A provider camera event (motion, person, and so on).</summary>
    CameraEvent,

    /// <summary>A recorded jamming incident.</summary>
    Jamming,

    /// <summary>A signal deviation hour on a camera.</summary>
    SignalAnomaly,

    /// <summary>A bit-rate flag on a camera.</summary>
    BitRate,
}

/// <summary>One row of the review Events tab, normalised across the four sources so they can share one list.</summary>
/// <param name="Kind">Which source the row came from.</param>
/// <param name="DeviceId">The camera the row belongs to.</param>
/// <param name="StartUtc">When the row starts (UTC). For a camera event, the time it occurred.</param>
/// <param name="EndUtc">When the row ends (UTC). Null for a camera event, which is a point in time.</param>
/// <param name="EventId">The camera event's id. Null for every other kind.</param>
/// <param name="Source">The original DTO the row was built from, for the detail view to read.</param>
public sealed record ReviewEventRow(
    ReviewEventRowKind Kind,
    Guid DeviceId,
    DateTime StartUtc,
    DateTime? EndUtc,
    Guid? EventId,
    object Source);

/// <summary>Merges the review Events tab sources into one list, newest first.</summary>
/// <remarks>
/// Pure mapping with no I/O, so the tab and its tests share one rule. Ties on start time are ordered by kind
/// (camera event, jamming, signal anomaly, bit rate) and then by device, so the list does not shuffle between renders.
/// </remarks>
public static class ReviewEventRows
{
    /// <summary>Builds the merged, sorted row list from the four sources.</summary>
    /// <param name="events">Camera events.</param>
    /// <param name="jamming">Jamming incidents.</param>
    /// <param name="anomalies">Signal anomaly hours.</param>
    /// <param name="bitRate">Bit-rate flags.</param>
    /// <returns>The rows, newest start first; empty when every source is empty.</returns>
    public static IReadOnlyList<ReviewEventRow> Merge(
        IEnumerable<EventDto> events,
        IEnumerable<JammingIncidentDto> jamming,
        IEnumerable<ReviewAnomaly> anomalies,
        IEnumerable<ReviewBitRateFlag> bitRate)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(jamming);
        ArgumentNullException.ThrowIfNull(anomalies);
        ArgumentNullException.ThrowIfNull(bitRate);

        var rows = new List<ReviewEventRow>();

        foreach (EventDto evt in events)
        {
            rows.Add(new ReviewEventRow(ReviewEventRowKind.CameraEvent, evt.DeviceId, evt.OccurredAtUtc, null, evt.Id, evt));
        }

        foreach (JammingIncidentDto incident in jamming)
        {
            rows.Add(new ReviewEventRow(ReviewEventRowKind.Jamming, incident.DeviceId, incident.StartUtc, incident.EndUtc, null, incident));
        }

        foreach (ReviewAnomaly anomaly in anomalies)
        {
            rows.Add(new ReviewEventRow(ReviewEventRowKind.SignalAnomaly, anomaly.DeviceId, anomaly.StartUtc, anomaly.EndUtc, null, anomaly));
        }

        foreach (ReviewBitRateFlag flag in bitRate)
        {
            rows.Add(new ReviewEventRow(ReviewEventRowKind.BitRate, flag.DeviceId, flag.StartUtc, flag.EndUtc, null, flag));
        }

        return rows
            .OrderByDescending(r => r.StartUtc)
            .ThenBy(r => r.Kind)
            .ThenBy(r => r.DeviceId)
            .ToList();
    }
}
