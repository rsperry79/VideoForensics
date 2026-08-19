#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Entities;

namespace KoenZomers.Ring.Api.Interfaces;

/// <summary>
/// Service for accessing Ring recordings and snapshots.
/// </summary>
public interface IRecordingService
{
    /// <summary>
    /// Gets the history of recorded events for a doorbot.
    /// </summary>
    Task<List<DoorbotHistoryEvent>> GetDoorbotHistory(
        int limit = 100,
        DateTimeOffset? dateRange = null,
        string? doorbotId = null);

    /// <summary>
    /// Downloads a recorded video from a doorbot history event.
    /// </summary>
    Task<DownloadRecording> GetDoorbotHistoryRecording(
        DoorbotHistoryEvent evt,
        string saveAs);

    /// <summary>
    /// Gets information about a recorded video without downloading it.
    /// </summary>
    Task<DoorbotHistoryEventRecording> GetDoorbotHistoryRecordingInfo(
        DoorbotHistoryEvent evt);

    /// <summary>
    /// Gets the latest snapshot for a doorbot.
    /// </summary>
    Task<Stream> GetLatestSnapshot(Doorbot doorbot, string saveAs);

    /// <summary>
    /// Updates the snapshot for a doorbot device.
    /// </summary>
    Task<bool> UpdateSnapshot(string doorbotId);

    /// <summary>
    /// Shares a recording by creating a shareable link.
    /// </summary>
    Task<string> ShareRecording(string recordingId);

    /// <summary>
    /// Gets events for a specific location.
    /// </summary>
    Task<List<LocationEvent>> GetLocationEvents(Guid locationId);
}
