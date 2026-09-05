
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Interfaces;

namespace VideoForensics.Providers.Ring.Clients;

/// <summary>
/// High-level client for downloading Ring videos and snapshots.
/// </summary>
public class VideoDownloadClient : IVideoDownloadClient
{
    private readonly IRecordingService _recordingService;
    private readonly IDeviceDiscoveryService _deviceService;

    public VideoDownloadClient(
        IRecordingService recordingService,
        IDeviceDiscoveryService deviceService)
    {
        _recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        _deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
    }

    public async Task<List<DoorbotHistoryEvent>> GetRecordingsAsync(
        int? limit = null,
        DateTimeOffset? dateRange = null,
        string? deviceId = null,
        string? eventKind = null,
        CancellationToken cancellationToken = default)
    {
        List<DoorbotHistoryEvent> recordings = await _recordingService.GetDoorbotHistory(
            limit ?? 100,
            dateRange,
            deviceId,
            cancellationToken);

        if (!string.IsNullOrEmpty(eventKind))
        {
            recordings = recordings.Where(r => r.Kind == eventKind).ToList();
        }

        return recordings;
    }

    public async Task<bool> DownloadRecordingAsync(
        DoorbotHistoryEvent recording,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (recording == null)
        {
            throw new ArgumentNullException(nameof(recording));
        }

        if (string.IsNullOrEmpty(outputPath))
        {
            throw new ArgumentException("Output path is required", nameof(outputPath));
        }

        try
        {
            _ = await _recordingService.GetDoorbotHistoryRecording(recording, outputPath, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DownloadSnapshotAsync(
        string deviceId,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            throw new ArgumentException("Device ID is required", nameof(deviceId));
        }

        if (string.IsNullOrEmpty(outputPath))
        {
            throw new ArgumentException("Output path is required", nameof(outputPath));
        }

        try
        {
            List<Doorbot> devices = await _deviceService.GetRingDevices(null, cancellationToken);
            Doorbot? device = devices.FirstOrDefault(d => d.DeviceId == deviceId);

            if (device == null)
            {
                return false;
            }

            _ = await _recordingService.GetLatestSnapshot(device, outputPath, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DoorbotHistoryEventRecording> GetRecordingInfoAsync(
        DoorbotHistoryEvent recording,
        CancellationToken cancellationToken = default)
    {
        return recording == null
            ? throw new ArgumentNullException(nameof(recording))
            : await _recordingService.GetDoorbotHistoryRecordingInfo(recording, cancellationToken);
    }

    public async Task<string> ShareRecordingAsync(string recordingId, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(recordingId)
            ? throw new ArgumentException("Recording ID is required", nameof(recordingId))
            : await _recordingService.ShareRecording(recordingId, cancellationToken);
    }
}
