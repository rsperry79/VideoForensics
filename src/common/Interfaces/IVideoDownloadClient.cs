#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Ring.Api.Entities;

namespace Ring.Api.Interfaces;

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
        string? eventKind = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a specific recording to the given output path.
    /// </summary>
    Task<bool> DownloadRecordingAsync(
        DoorbotHistoryEvent recording,
        string outputPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a snapshot from a device.
    /// </summary>
    Task<bool> DownloadSnapshotAsync(
        string deviceId,
        string outputPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about a recording without downloading it.
    /// </summary>
    Task<DoorbotHistoryEventRecording> GetRecordingInfoAsync(
        DoorbotHistoryEvent recording,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Shares a recording by creating a shareable link.
    /// </summary>
    Task<string> ShareRecordingAsync(string recordingId, CancellationToken cancellationToken = default);
}
