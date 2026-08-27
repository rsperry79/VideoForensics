using VideoForensics.Data.Common.Entities;
using Xunit;

namespace VideoForensics.Data.Common.Tests;

public class EventEntityTests
{
    [Fact]
    public void Event_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var providerEventId = "ring_event_123";
        var eventType = "motion";
        var occurredAtUtc = DateTime.UtcNow.AddHours(-1);
        var snapshotUrl = "https://example.com/snapshot.jpg";
        var metadataJson = "{\"zone\": \"front_door\"}";
        var discoveredAtUtc = DateTime.UtcNow;

        // Act
        var evt = new Event
        {
            Id = id,
            DeviceId = deviceId,
            ProviderEventId = providerEventId,
            EventType = eventType,
            OccurredAtUtc = occurredAtUtc,
            SnapshotUrl = snapshotUrl,
            MetadataJson = metadataJson,
            DiscoveredAtUtc = discoveredAtUtc
        };

        // Assert
        Assert.Equal(id, evt.Id);
        Assert.Equal(deviceId, evt.DeviceId);
        Assert.Equal(providerEventId, evt.ProviderEventId);
        Assert.Equal(eventType, evt.EventType);
        Assert.Equal(occurredAtUtc, evt.OccurredAtUtc);
        Assert.Equal(snapshotUrl, evt.SnapshotUrl);
        Assert.Equal(metadataJson, evt.MetadataJson);
        Assert.Equal(discoveredAtUtc, evt.DiscoveredAtUtc);
    }

    [Fact]
    public void Event_WithoutOptionalFields_ReturnsNullValues()
    {
        // Arrange & Act
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            ProviderEventId = "ring_event_123",
            EventType = "motion",
            OccurredAtUtc = DateTime.UtcNow.AddHours(-1),
            SnapshotUrl = null,
            MetadataJson = null,
            DiscoveredAtUtc = DateTime.UtcNow
        };

        // Assert
        Assert.Null(evt.SnapshotUrl);
        Assert.Null(evt.MetadataJson);
    }

    [Fact]
    public void Event_WithDifferentEventTypes_Distinguishes()
    {
        // Arrange
        var deviceId = Guid.NewGuid();

        // Act
        var motionEvent = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            ProviderEventId = "event_motion",
            EventType = "motion",
            OccurredAtUtc = DateTime.UtcNow,
            SnapshotUrl = null,
            MetadataJson = null,
            DiscoveredAtUtc = DateTime.UtcNow
        };

        var doorbellEvent = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            ProviderEventId = "event_doorbell",
            EventType = "doorbell",
            OccurredAtUtc = DateTime.UtcNow,
            SnapshotUrl = null,
            MetadataJson = null,
            DiscoveredAtUtc = DateTime.UtcNow
        };

        // Assert
        Assert.NotEqual(motionEvent.EventType, doorbellEvent.EventType);
    }

    [Fact]
    public void DeviceConfigSnapshot_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var motionDetectionEnabled = true;
        var motionSensitivity = "high";
        var recordingMode = "continuous";
        var customSettingsJson = "{\"nightVision\": true}";
        var capturedAtUtc = DateTime.UtcNow;
        var source = DeviceConfigSource.Fetched;

        // Act
        var snapshot = new DeviceConfigSnapshot
        {
            Id = id,
            DeviceId = deviceId,
            MotionDetectionEnabled = motionDetectionEnabled,
            MotionSensitivity = motionSensitivity,
            RecordingMode = recordingMode,
            CustomSettingsJson = customSettingsJson,
            CapturedAtUtc = capturedAtUtc,
            Source = source
        };

        // Assert
        Assert.Equal(id, snapshot.Id);
        Assert.Equal(deviceId, snapshot.DeviceId);
        Assert.True(snapshot.MotionDetectionEnabled);
        Assert.Equal(motionSensitivity, snapshot.MotionSensitivity);
        Assert.Equal(recordingMode, snapshot.RecordingMode);
        Assert.Equal(customSettingsJson, snapshot.CustomSettingsJson);
        Assert.Equal(capturedAtUtc, snapshot.CapturedAtUtc);
        Assert.Equal(DeviceConfigSource.Fetched, snapshot.Source);
    }

    [Fact]
    public void DeviceConfigSnapshot_WithoutOptionalFields_ReturnsNullValues()
    {
        // Arrange & Act
        var snapshot = new DeviceConfigSnapshot
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            MotionDetectionEnabled = null,
            MotionSensitivity = null,
            RecordingMode = null,
            CustomSettingsJson = null,
            CapturedAtUtc = DateTime.UtcNow,
            Source = DeviceConfigSource.Fetched
        };

        // Assert
        Assert.Null(snapshot.MotionDetectionEnabled);
        Assert.Null(snapshot.MotionSensitivity);
        Assert.Null(snapshot.RecordingMode);
        Assert.Null(snapshot.CustomSettingsJson);
    }

    [Fact]
    public void DeviceConfigSnapshot_WithSourceApplied_RoundsTrip()
    {
        // Arrange & Act
        var snapshot = new DeviceConfigSnapshot
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            MotionDetectionEnabled = false,
            MotionSensitivity = "medium",
            RecordingMode = "motion",
            CustomSettingsJson = null,
            CapturedAtUtc = DateTime.UtcNow,
            Source = DeviceConfigSource.Applied
        };

        // Assert
        Assert.Equal(DeviceConfigSource.Applied, snapshot.Source);
        Assert.False(snapshot.MotionDetectionEnabled);
    }

    [Fact]
    public void DeviceConfigSnapshot_ConfigHistory_PreservesMultipleSnapshots()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var fetchedTime = DateTime.UtcNow.AddDays(-1);
        var appliedTime = DateTime.UtcNow;

        // Act
        var fetchedSnapshot = new DeviceConfigSnapshot
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            MotionDetectionEnabled = true,
            MotionSensitivity = "high",
            RecordingMode = "continuous",
            CustomSettingsJson = null,
            CapturedAtUtc = fetchedTime,
            Source = DeviceConfigSource.Fetched
        };

        var appliedSnapshot = new DeviceConfigSnapshot
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            MotionDetectionEnabled = false,
            MotionSensitivity = "medium",
            RecordingMode = "motion",
            CustomSettingsJson = null,
            CapturedAtUtc = appliedTime,
            Source = DeviceConfigSource.Applied
        };

        // Assert
        Assert.Equal(deviceId, fetchedSnapshot.DeviceId);
        Assert.Equal(deviceId, appliedSnapshot.DeviceId);
        Assert.NotEqual(fetchedSnapshot.Source, appliedSnapshot.Source);
        Assert.True(fetchedSnapshot.CapturedAtUtc < appliedSnapshot.CapturedAtUtc);
    }
}
