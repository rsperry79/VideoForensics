using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Contracts;

/// <summary>Loads what the review Detail tab shows: event detail rows, suspected jamming, and anomalies.</summary>
public interface IReviewDetailLoader
{
    /// <summary>
    /// Returns the detail rows for the given events, keyed by event id. Events with no detail rows are left out.
    /// </summary>
    /// <param name="eventIds">Events to load. Duplicates are ignored; an empty set returns an empty dictionary.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyDictionary<Guid, EventDetail>> LoadEventDetailsAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken ct);

    /// <summary>
    /// Returns the suspected jamming incidents recorded by <paramref name="deviceIds"/> in the range, newest first.
    /// </summary>
    /// <param name="deviceIds">Cameras to query. Duplicates are ignored.</param>
    /// <param name="fromUtc">Start of the range (UTC).</param>
    /// <param name="toUtc">End of the range (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<JammingIncidentRecord>> LoadJammingAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    /// <summary>
    /// Returns the notable signal deviations (every level except normal) for <paramref name="deviceIds"/> in the range, newest first.
    /// </summary>
    /// <param name="deviceIds">Cameras to query. Duplicates are ignored.</param>
    /// <param name="fromUtc">Start of the range (UTC).</param>
    /// <param name="toUtc">End of the range (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<ReviewAnomaly>> LoadAnomaliesAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
}

/// <summary>One notable signal deviation on a camera, spanning the hour it was flagged in.</summary>
/// <param name="DeviceId">The camera.</param>
/// <param name="StartUtc">Start of the flagged hour (UTC).</param>
/// <param name="EndUtc">End of the flagged hour (UTC).</param>
/// <param name="Level">How far the readings sat from the camera's mean. Never the normal level.</param>
public sealed record ReviewAnomaly(Guid DeviceId, DateTime StartUtc, DateTime EndUtc, SignalDeviationLevel Level);
