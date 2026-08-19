#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Interfaces;
using KoenZomers.Ring.Api.Entities;

namespace KoenZomers.Ring.Api.Clients;

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
        string? eventKind = null)
    {
        var recordings = await _recordingService.GetDoorbotHistory(
            limit ?? 100,
            dateRange,
            deviceId);

        if (!string.IsNullOrEmpty(eventKind))
        {
            recordings = recordings.Where(r => r.Kind == eventKind).ToList();
        }

        return recordings;
    }

    public async Task<bool> DownloadRecordingAsync(
        DoorbotHistoryEvent recording,
        string outputPath)
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
            await _recordingService.GetDoorbotHistoryRecording(recording, outputPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DownloadSnapshotAsync(
        string deviceId,
        string outputPath)
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
            var devices = await _deviceService.GetRingDevices();
            var device = devices.FirstOrDefault(d => d.DeviceId == deviceId);

            if (device == null)
            {
                return false;
            }

            await _recordingService.GetLatestSnapshot(device, outputPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DoorbotHistoryEventRecording> GetRecordingInfoAsync(
        DoorbotHistoryEvent recording)
    {
        if (recording == null)
        {
            throw new ArgumentNullException(nameof(recording));
        }

        return await _recordingService.GetDoorbotHistoryRecordingInfo(recording);
    }

    public async Task<string> ShareRecordingAsync(string recordingId)
    {
        if (string.IsNullOrEmpty(recordingId))
        {
            throw new ArgumentException("Recording ID is required", nameof(recordingId));
        }

        return await _recordingService.ShareRecording(recordingId);
    }
}
