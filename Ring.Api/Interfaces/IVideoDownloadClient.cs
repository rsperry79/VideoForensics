#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Entities;

namespace KoenZomers.Ring.Api.Interfaces;

/// <summary>
/// High-level client for downloading Ring videos and snapshots.
/// </summary>
public interface IVideoDownloadClient
{
    /// <summary>
    /// Gets recordings matching the specified filter criteria.
    /// </summary>
    Task<List<DoorbotHistoryEvent>> GetRecordingsAsync(
        int? limit = null,
        DateTimeOffset? dateRange = null,
        string? deviceId = null,
        string? eventKind = null);

    /// <summary>
    /// Downloads a specific recording to the given output path.
    /// </summary>
    Task<bool> DownloadRecordingAsync(
        DoorbotHistoryEvent recording,
        string outputPath);

    /// <summary>
    /// Downloads a snapshot from a device.
    /// </summary>
    Task<bool> DownloadSnapshotAsync(
        string deviceId,
        string outputPath);

    /// <summary>
    /// Gets information about a recording without downloading it.
    /// </summary>
    Task<DoorbotHistoryEventRecording> GetRecordingInfoAsync(
        DoorbotHistoryEvent recording);

    /// <summary>
    /// Shares a recording by creating a shareable link.
    /// </summary>
    Task<string> ShareRecordingAsync(string recordingId);
}
