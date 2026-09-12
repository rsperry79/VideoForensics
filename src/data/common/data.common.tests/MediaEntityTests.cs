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
        string fileName = "video.mp4";
        string filePath = "/path/to/video.mp4";
        string mediaFormat = "video/mp4";
        long fileSizeBytes = 1024000L;
        var recordedAtUtc = DateTime.UtcNow.AddHours(-2);
        var downloadedAtUtc = DateTime.UtcNow;
        string sha256Hash = "abc123def456";
        string videoCodec = "h264";
        string audioCodec = "aac";
        string resolution = "1920x1080";
        decimal frameRate = 30.0m;
        bool integrityVerified = true;
        var lastVerifiedAtUtc = DateTime.UtcNow;
        bool isPurged = false;

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
        string purgeReason = "Retention policy expired";

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
        string providerEventId = "event-789";
        string eventType = "motion";
        bool answered = true;
        bool favorite = false;
        var eventOccurredAtUtc = DateTime.UtcNow.AddHours(-1);
        string recordingStatus = "ready";
        var downloadStartedUtc = DateTime.UtcNow.AddMinutes(-30);
        var downloadCompletedUtc = DateTime.UtcNow;
        bool success = true;
        int attemptCount = 1;
        string appVersion = "1.0.0";

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
        string errorMessage = "File not found on provider";

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
    public void DeviceHealth_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        bool isOnline = true;
        decimal batteryPercentage = 75.5m;
        int wifiSignalRssi = -45;
        string wifiName = "HomeNetwork";
        string firmwareVersion = "2.8.32";
        var capturedAtUtc = DateTime.UtcNow;

        // Act
        var snapshot = new DeviceHealth
        {
            Id = id,
            DeviceId = deviceId,
            IsOnline = isOnline,
            BatteryPercentage = batteryPercentage,
            WifiSignalRssi = wifiSignalRssi,
            WifiName = wifiName,
            FirmwareVersion = firmwareVersion,
            CapturedAtUtc = capturedAtUtc
        };

        // Assert
        Assert.Equal(id, snapshot.Id);
        Assert.Equal(deviceId, snapshot.DeviceId);
        Assert.True(snapshot.IsOnline);
        Assert.Equal(batteryPercentage, snapshot.BatteryPercentage);
        Assert.Equal(wifiSignalRssi, snapshot.WifiSignalRssi);
        Assert.Equal(wifiName, snapshot.WifiName);
        Assert.Equal(firmwareVersion, snapshot.FirmwareVersion);
        Assert.Equal(capturedAtUtc, snapshot.CapturedAtUtc);
    }

    [Fact]
    public void DeviceHealth_WithOptionalNullValues_ReturnsNulls()
    {
        // Arrange & Act
        var snapshot = new DeviceHealth
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            IsOnline = null,
            BatteryPercentage = null,
            WifiSignalRssi = null,
            WifiName = null,
            FirmwareVersion = null,
            CapturedAtUtc = DateTime.UtcNow
        };

        // Assert
        Assert.Null(snapshot.IsOnline);
        Assert.Null(snapshot.BatteryPercentage);
        Assert.Null(snapshot.WifiSignalRssi);
        Assert.Null(snapshot.WifiName);
        Assert.Null(snapshot.FirmwareVersion);
    }

    [Fact]
    public void AiAnalysisSnapshot_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var downloadEventId = Guid.NewGuid();
        bool personDetected = true;
        decimal confidenceScore = 0.95m;
        string fullDescription = "Person detected at front door";

        // Act
        var snapshot = new AiAnalysisSnapshot
        {
            Id = id,
            DownloadEventId = downloadEventId,
            PersonDetected = personDetected,
            ConfidenceScore = confidenceScore,
            FullDescription = fullDescription
        };

        // Assert
        Assert.Equal(id, snapshot.Id);
        Assert.Equal(downloadEventId, snapshot.DownloadEventId);
        Assert.True(snapshot.PersonDetected);
        Assert.Equal(confidenceScore, snapshot.ConfidenceScore);
        Assert.Equal(fullDescription, snapshot.FullDescription);
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
            FullDescription = null
        };

        // Assert
        Assert.Null(snapshot.PersonDetected);
        Assert.Null(snapshot.ConfidenceScore);
        Assert.Null(snapshot.FullDescription);
    }
}
