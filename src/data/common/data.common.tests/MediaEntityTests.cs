using VideoForensics.Data.Common.Entities;
using Xunit;

namespace VideoForensics.Data.Common.Tests;

public class MediaEntityTests
{
    [Fact]
    public void MediaItem_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var downloadEventId = Guid.NewGuid();
        var fileName = "video.mp4";
        var filePath = "/path/to/video.mp4";
        var mediaFormat = "video/mp4";
        var fileSizeBytes = 1024000L;
        var recordedAtUtc = DateTime.UtcNow.AddHours(-2);
        var downloadedAtUtc = DateTime.UtcNow;
        var sha256Hash = "abc123def456";
        var videoCodec = "h264";
        var audioCodec = "aac";
        var resolution = "1920x1080";
        var frameRate = 30.0m;
        var integrityVerified = true;
        var lastVerifiedAtUtc = DateTime.UtcNow;
        var isPurged = false;

        // Act
        var mediaItem = new MediaItem
        {
            Id = id,
            DeviceId = deviceId,
            DownloadEventId = downloadEventId,
            FileName = fileName,
            FilePath = filePath,
            MediaFormat = mediaFormat,
            FileSizeBytes = fileSizeBytes,
            RecordedAtUtc = recordedAtUtc,
            DownloadedAtUtc = downloadedAtUtc,
            Sha256Hash = sha256Hash,
            VideoCodec = videoCodec,
            AudioCodec = audioCodec,
            Resolution = resolution,
            FrameRate = frameRate,
            IntegrityVerified = integrityVerified,
            LastVerifiedAtUtc = lastVerifiedAtUtc,
            IsPurged = isPurged,
            PurgedAtUtc = null,
            PurgeReason = null
        };

        // Assert
        Assert.Equal(id, mediaItem.Id);
        Assert.Equal(deviceId, mediaItem.DeviceId);
        Assert.Equal(downloadEventId, mediaItem.DownloadEventId);
        Assert.Equal(fileName, mediaItem.FileName);
        Assert.Equal(filePath, mediaItem.FilePath);
        Assert.Equal(mediaFormat, mediaItem.MediaFormat);
        Assert.Equal(fileSizeBytes, mediaItem.FileSizeBytes);
        Assert.Equal(recordedAtUtc, mediaItem.RecordedAtUtc);
        Assert.Equal(downloadedAtUtc, mediaItem.DownloadedAtUtc);
        Assert.Equal(sha256Hash, mediaItem.Sha256Hash);
        Assert.Equal(videoCodec, mediaItem.VideoCodec);
        Assert.Equal(audioCodec, mediaItem.AudioCodec);
        Assert.Equal(resolution, mediaItem.Resolution);
        Assert.Equal(frameRate, mediaItem.FrameRate);
        Assert.True(mediaItem.IntegrityVerified);
        Assert.Equal(lastVerifiedAtUtc, mediaItem.LastVerifiedAtUtc);
        Assert.False(mediaItem.IsPurged);
    }

    [Fact]
    public void MediaItem_WithoutOptionalCodecMetadata_ReturnsNullValues()
    {
        // Arrange & Act
        var mediaItem = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            DownloadEventId = null,
            FileName = "video.mp4",
            FilePath = "/path/to/video.mp4",
            MediaFormat = "video/mp4",
            FileSizeBytes = 1024000L,
            RecordedAtUtc = DateTime.UtcNow.AddHours(-2),
            DownloadedAtUtc = DateTime.UtcNow,
            Sha256Hash = "abc123def456",
            VideoCodec = null,
            AudioCodec = null,
            Resolution = null,
            FrameRate = null,
            IntegrityVerified = false,
            LastVerifiedAtUtc = null,
            IsPurged = false,
            PurgedAtUtc = null,
            PurgeReason = null
        };

        // Assert
        Assert.Null(mediaItem.DownloadEventId);
        Assert.Null(mediaItem.VideoCodec);
        Assert.Null(mediaItem.AudioCodec);
        Assert.Null(mediaItem.Resolution);
        Assert.Null(mediaItem.FrameRate);
        Assert.Null(mediaItem.LastVerifiedAtUtc);
    }

    [Fact]
    public void MediaItem_WhenPurged_ContainsPurgeMetadata()
    {
        // Arrange
        var purgedAtUtc = DateTime.UtcNow;
        var purgeReason = "Retention policy expired";

        // Act
        var mediaItem = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            DownloadEventId = null,
            FileName = "video.mp4",
            FilePath = "/path/to/video.mp4",
            MediaFormat = "video/mp4",
            FileSizeBytes = 1024000L,
            RecordedAtUtc = DateTime.UtcNow.AddHours(-2),
            DownloadedAtUtc = DateTime.UtcNow.AddDays(-30),
            Sha256Hash = "abc123def456",
            VideoCodec = null,
            AudioCodec = null,
            Resolution = null,
            FrameRate = null,
            IntegrityVerified = false,
            LastVerifiedAtUtc = null,
            IsPurged = true,
            PurgedAtUtc = purgedAtUtc,
            PurgeReason = purgeReason
        };

        // Assert
        Assert.True(mediaItem.IsPurged);
        Assert.Equal(purgedAtUtc, mediaItem.PurgedAtUtc);
        Assert.Equal(purgeReason, mediaItem.PurgeReason);
    }

    [Fact]
    public void DownloadEvent_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var providerEventId = "event-789";
        var eventType = "motion";
        var answered = true;
        var favorite = false;
        var eventOccurredAtUtc = DateTime.UtcNow.AddHours(-1);
        var recordingStatus = "ready";
        var downloadStartedUtc = DateTime.UtcNow.AddMinutes(-30);
        var downloadCompletedUtc = DateTime.UtcNow;
        var success = true;
        var attemptCount = 1;
        var appVersion = "1.0.0";

        // Act
        var downloadEvent = new DownloadEvent
        {
            Id = id,
            DeviceId = deviceId,
            ProviderEventId = providerEventId,
            EventType = eventType,
            Answered = answered,
            Favorite = favorite,
            EventOccurredAtUtc = eventOccurredAtUtc,
            RecordingStatus = recordingStatus,
            DownloadStartedUtc = downloadStartedUtc,
            DownloadCompletedUtc = downloadCompletedUtc,
            Success = success,
            AttemptCount = attemptCount,
            ErrorMessage = null,
            AppVersion = appVersion
        };

        // Assert
        Assert.Equal(id, downloadEvent.Id);
        Assert.Equal(deviceId, downloadEvent.DeviceId);
        Assert.Equal(providerEventId, downloadEvent.ProviderEventId);
        Assert.Equal(eventType, downloadEvent.EventType);
        Assert.True(downloadEvent.Answered);
        Assert.False(downloadEvent.Favorite);
        Assert.Equal(eventOccurredAtUtc, downloadEvent.EventOccurredAtUtc);
        Assert.Equal(recordingStatus, downloadEvent.RecordingStatus);
        Assert.Equal(downloadStartedUtc, downloadEvent.DownloadStartedUtc);
        Assert.Equal(downloadCompletedUtc, downloadEvent.DownloadCompletedUtc);
        Assert.True(downloadEvent.Success);
        Assert.Equal(attemptCount, downloadEvent.AttemptCount);
        Assert.Equal(appVersion, downloadEvent.AppVersion);
    }

    [Fact]
    public void DownloadEvent_FailedDownload_ContainsErrorMessage()
    {
        // Arrange
        var errorMessage = "File not found on provider";

        // Act
        var downloadEvent = new DownloadEvent
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            ProviderEventId = "event-789",
            EventType = "motion",
            Answered = false,
            Favorite = false,
            EventOccurredAtUtc = DateTime.UtcNow.AddHours(-1),
            RecordingStatus = null,
            DownloadStartedUtc = DateTime.UtcNow.AddMinutes(-30),
            DownloadCompletedUtc = DateTime.UtcNow,
            Success = false,
            AttemptCount = 3,
            ErrorMessage = errorMessage,
            AppVersion = "1.0.0"
        };

        // Assert
        Assert.False(downloadEvent.Success);
        Assert.Equal(3, downloadEvent.AttemptCount);
        Assert.Equal(errorMessage, downloadEvent.ErrorMessage);
    }

    [Fact]
    public void DeviceHealthSnapshot_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var downloadEventId = Guid.NewGuid();
        var connected = true;
        var batteryPercentage = 75.5m;
        var rssi = -45;
        var wifiName = "HomeNetwork";
        var firmwareVersion = "2.8.32";
        var capturedAtUtc = DateTime.UtcNow;

        // Act
        var snapshot = new DeviceHealthSnapshot
        {
            Id = id,
            DownloadEventId = downloadEventId,
            Connected = connected,
            BatteryPercentage = batteryPercentage,
            Rssi = rssi,
            WifiName = wifiName,
            FirmwareVersion = firmwareVersion,
            CapturedAtUtc = capturedAtUtc
        };

        // Assert
        Assert.Equal(id, snapshot.Id);
        Assert.Equal(downloadEventId, snapshot.DownloadEventId);
        Assert.True(snapshot.Connected);
        Assert.Equal(batteryPercentage, snapshot.BatteryPercentage);
        Assert.Equal(rssi, snapshot.Rssi);
        Assert.Equal(wifiName, snapshot.WifiName);
        Assert.Equal(firmwareVersion, snapshot.FirmwareVersion);
        Assert.Equal(capturedAtUtc, snapshot.CapturedAtUtc);
    }

    [Fact]
    public void DeviceHealthSnapshot_WithOptionalNullValues_ReturnsNulls()
    {
        // Arrange & Act
        var snapshot = new DeviceHealthSnapshot
        {
            Id = Guid.NewGuid(),
            DownloadEventId = Guid.NewGuid(),
            Connected = null,
            BatteryPercentage = null,
            Rssi = null,
            WifiName = null,
            FirmwareVersion = null,
            CapturedAtUtc = DateTime.UtcNow
        };

        // Assert
        Assert.Null(snapshot.Connected);
        Assert.Null(snapshot.BatteryPercentage);
        Assert.Null(snapshot.Rssi);
        Assert.Null(snapshot.WifiName);
        Assert.Null(snapshot.FirmwareVersion);
    }

    [Fact]
    public void AiAnalysisSnapshot_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var downloadEventId = Guid.NewGuid();
        var personDetected = true;
        var confidenceScore = 0.95m;
        var fullDescription = "Person detected at front door";
        var tagsJson = "[\"person\", \"adult\"]";
        var motionZonesJson = "[{\"x\": 0, \"y\": 0, \"width\": 100, \"height\": 100}]";

        // Act
        var snapshot = new AiAnalysisSnapshot
        {
            Id = id,
            DownloadEventId = downloadEventId,
            PersonDetected = personDetected,
            ConfidenceScore = confidenceScore,
            FullDescription = fullDescription,
            TagsJson = tagsJson,
            MotionZonesJson = motionZonesJson
        };

        // Assert
        Assert.Equal(id, snapshot.Id);
        Assert.Equal(downloadEventId, snapshot.DownloadEventId);
        Assert.True(snapshot.PersonDetected);
        Assert.Equal(confidenceScore, snapshot.ConfidenceScore);
        Assert.Equal(fullDescription, snapshot.FullDescription);
        Assert.Equal(tagsJson, snapshot.TagsJson);
        Assert.Equal(motionZonesJson, snapshot.MotionZonesJson);
    }

    [Fact]
    public void AiAnalysisSnapshot_WithNoAnalysisResults_ReturnsNullValues()
    {
        // Arrange & Act
        var snapshot = new AiAnalysisSnapshot
        {
            Id = Guid.NewGuid(),
            DownloadEventId = Guid.NewGuid(),
            PersonDetected = null,
            ConfidenceScore = null,
            FullDescription = null,
            TagsJson = null,
            MotionZonesJson = null
        };

        // Assert
        Assert.Null(snapshot.PersonDetected);
        Assert.Null(snapshot.ConfidenceScore);
        Assert.Null(snapshot.FullDescription);
        Assert.Null(snapshot.TagsJson);
        Assert.Null(snapshot.MotionZonesJson);
    }
}
