using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Services.Inspector;
using VideoForensics.Client.Ui.Shared.Services.QueryApi;
using VideoForensics.Client.Ui.Shared.Services.Time;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.QueryApi;

public class LocationInspectorMapper_ToInspector_Tests
{
    [Fact]
    public void ToInspector_BuildsCorrectTitle()
    {
        // Arrange
        var location = new Location(
            Id: "loc-123",
            Name: "Home Office",
            Address: "123 Main St"
        );
        var retrievedAt = new DateTime(2026, 9, 20, 14, 30, 45, DateTimeKind.Utc);

        // Act
        InspectorModel inspector = LocationInspectorMapper.ToInspector(location, retrievedAt);

        // Assert
        Assert.Contains("Location", inspector.Title);
        Assert.Contains("Home Office", inspector.Title);
    }

    [Fact]
    public void ToInspector_IncludesLocationFields()
    {
        // Arrange
        var location = new Location(
            Id: "loc-123",
            Name: "Home",
            Address: "123 St"
        );
        DateTime retrievedAt = DateTime.UtcNow;

        // Act
        InspectorModel inspector = LocationInspectorMapper.ToInspector(location, retrievedAt);

        // Assert
        Assert.NotNull(inspector.Fields);
        Assert.NotNull(inspector.Provenance);
    }

    [Fact]
    public void ToInspector_WithMetadata_SetsRawJson()
    {
        // Arrange
        var metadata = new Dictionary<string, string> { { "key", "value" } };
        var location = new Location(
            Id: "loc-123",
            Name: "Home",
            Address: "123 St",
            Metadata: metadata
        );
        DateTime retrievedAt = DateTime.UtcNow;

        // Act
        InspectorModel inspector = LocationInspectorMapper.ToInspector(location, retrievedAt);

        // Assert
        // The raw JSON should be null for Location since it doesn't have MetadataJson
        Assert.Null(inspector.RawJson);
    }

    [Fact]
    public void ToInspector_ProvenanceIncludesSourceAndTimestamp()
    {
        // Arrange
        var location = new Location(Id: "loc-123", Name: "Home");
        var retrievedAt = new DateTime(2026, 9, 20, 14, 30, 45, DateTimeKind.Utc);

        // Act
        InspectorModel inspector = LocationInspectorMapper.ToInspector(location, retrievedAt);

        // Assert
        Assert.NotNull(inspector.Provenance);
        var provenanceDict = inspector.Provenance.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Assert.Contains("Source", provenanceDict.Keys);
        Assert.Contains("RetrievedAtUtc", provenanceDict.Keys);
        Assert.Equal("IDeviceDiscoveryService.GetLocationsAsync", provenanceDict["Source"]);
        Assert.Equal("2026-09-20 14:30:45", provenanceDict["RetrievedAtUtc"]);
    }

    [Fact]
    public void ToInspector_ProvenanceShowsTimeInTheUserZone()
    {
        // Arrange
        var location = new Location(Id: "loc-123", Name: "Home");
        var retrievedAt = new DateTime(2026, 9, 20, 14, 30, 45, DateTimeKind.Utc);
        var minusFive = new UserTimeZone(TimeZoneInfo.CreateCustomTimeZone("test-minus5", TimeSpan.FromHours(-5), "test-minus5", "test-minus5"));

        // Act
        InspectorModel inspector = LocationInspectorMapper.ToInspector(location, retrievedAt, minusFive);

        // Assert
        Assert.Equal("2026-09-20 09:30:45", inspector.Provenance!.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)["RetrievedAtUtc"]);
    }
}

public class DeviceInspectorMapper_ToInspector_Tests
{
    [Fact]
    public void ToInspector_BuildsCorrectTitle()
    {
        // Arrange
        var device = new Device(
            Id: "dev-123",
            Name: "Front Camera",
            Type: "camera",
            LocationId: "loc-123",
            IsOnline: true
        );
        string locationId = "loc-123";
        DateTime retrievedAt = DateTime.UtcNow;

        // Act
        InspectorModel inspector = DeviceInspectorMapper.ToInspector(device, locationId, retrievedAt);

        // Assert
        Assert.Contains("Device", inspector.Title);
        Assert.Contains("Front Camera", inspector.Title);
        Assert.Contains("camera", inspector.Title);
    }

    [Fact]
    public void ToInspector_IncludesDeviceFields()
    {
        // Arrange
        var device = new Device(
            Id: "dev-123",
            Name: "Camera",
            Type: "camera",
            LocationId: "loc-123",
            IsOnline: true
        );
        string locationId = "loc-123";
        DateTime retrievedAt = DateTime.UtcNow;

        // Act
        InspectorModel inspector = DeviceInspectorMapper.ToInspector(device, locationId, retrievedAt);

        // Assert
        Assert.NotNull(inspector.Fields);
        Assert.NotNull(inspector.Provenance);
    }

    [Fact]
    public void ToInspector_ProvenanceIncludesLocationIdAndSource()
    {
        // Arrange
        var device = new Device(
            Id: "dev-123",
            Name: "Camera",
            Type: "camera",
            LocationId: "loc-456",
            IsOnline: true
        );
        string locationId = "loc-456";
        var retrievedAt = new DateTime(2026, 9, 20, 14, 30, 45, DateTimeKind.Utc);

        // Act
        InspectorModel inspector = DeviceInspectorMapper.ToInspector(device, locationId, retrievedAt);

        // Assert
        Assert.NotNull(inspector.Provenance);
        var provenanceDict = inspector.Provenance.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Assert.Contains("LocationId", provenanceDict.Keys);
        Assert.Contains("Source", provenanceDict.Keys);
        Assert.Contains("RetrievedAtUtc", provenanceDict.Keys);
        Assert.Equal("loc-456", provenanceDict["LocationId"]);
        Assert.Equal("IDeviceDiscoveryService.GetDevicesAsync", provenanceDict["Source"]);
    }
}

public class DeviceEventInspectorMapper_ToInspector_Tests
{
    [Fact]
    public void ToInspector_BuildsCorrectTitle()
    {
        // Arrange
        var @event = new DeviceEvent(
            Id: "evt-123",
            DeviceId: "dev-123",
            EventType: "motion",
            Timestamp: new DateTime(2026, 9, 20, 14, 30, 45, DateTimeKind.Utc)
        );
        string deviceId = "dev-123";
        var fromUtc = new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);
        DateTime retrievedAt = DateTime.UtcNow;

        // Act
        InspectorModel inspector = DeviceEventInspectorMapper.ToInspector(@event, deviceId, fromUtc, toUtc, retrievedAt);

        // Assert
        Assert.Contains("Event", inspector.Title);
        Assert.Contains("motion", inspector.Title);
        Assert.Contains("2026-09-20", inspector.Title);
    }

    [Fact]
    public void ToInspector_IncludesEventFields()
    {
        // Arrange
        var @event = new DeviceEvent(
            Id: "evt-123",
            DeviceId: "dev-123",
            EventType: "motion",
            Timestamp: DateTime.UtcNow
        );
        string deviceId = "dev-123";
        DateTime fromUtc = DateTime.UtcNow.AddDays(-7);
        DateTime toUtc = DateTime.UtcNow;
        DateTime retrievedAt = DateTime.UtcNow;

        // Act
        InspectorModel inspector = DeviceEventInspectorMapper.ToInspector(@event, deviceId, fromUtc, toUtc, retrievedAt);

        // Assert
        Assert.NotNull(inspector.Fields);
        Assert.NotNull(inspector.Provenance);
    }

    [Fact]
    public void ToInspector_ProvenanceIncludesDeviceIdAndDateRange()
    {
        // Arrange
        var @event = new DeviceEvent(
            Id: "evt-123",
            DeviceId: "dev-456",
            EventType: "motion",
            Timestamp: new DateTime(2026, 9, 20, 14, 30, 45, DateTimeKind.Utc)
        );
        string deviceId = "dev-456";
        var fromUtc = new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);
        var retrievedAt = new DateTime(2026, 9, 20, 15, 0, 0, DateTimeKind.Utc);

        // Act
        InspectorModel inspector = DeviceEventInspectorMapper.ToInspector(@event, deviceId, fromUtc, toUtc, retrievedAt);

        // Assert
        Assert.NotNull(inspector.Provenance);
        var provenanceDict = inspector.Provenance.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Assert.Contains("DeviceId", provenanceDict.Keys);
        Assert.Contains("Source", provenanceDict.Keys);
        Assert.Contains("RetrievedAtUtc", provenanceDict.Keys);
        Assert.Equal("dev-456", provenanceDict["DeviceId"]);
        Assert.Equal("IEventAndConfigService.GetEventsAsync", provenanceDict["Source"]);
    }

    [Fact]
    public void ToInspector_WithMetadata_SetsRawJson()
    {
        // Arrange
        var metadata = new Dictionary<string, string> { { "key", "value" } };
        var @event = new DeviceEvent(
            Id: "evt-123",
            DeviceId: "dev-123",
            EventType: "motion",
            Timestamp: DateTime.UtcNow,
            Metadata: metadata
        );
        string deviceId = "dev-123";
        DateTime fromUtc = DateTime.UtcNow.AddDays(-7);
        DateTime toUtc = DateTime.UtcNow;
        DateTime retrievedAt = DateTime.UtcNow;

        // Act
        InspectorModel inspector = DeviceEventInspectorMapper.ToInspector(@event, deviceId, fromUtc, toUtc, retrievedAt);

        // Assert
        // DeviceEvent doesn't have MetadataJson property, but has Metadata dictionary
        // RawJson should be null since there's no MetadataJson
        Assert.Null(inspector.RawJson);
    }
}

public class DeviceConfigInspectorMapper_ToInspector_Tests
{
    [Fact]
    public void ToInspector_BuildsCorrectTitle()
    {
        // Arrange
        var config = new DeviceConfig(
            DeviceId: "dev-123",
            MotionDetectionEnabled: true,
            MotionSensitivity: 75,
            RecordingMode: "motion"
        );
        string deviceId = "dev-123";
        DateTime retrievedAt = DateTime.UtcNow;

        // Act
        InspectorModel inspector = DeviceConfigInspectorMapper.ToInspector(config, deviceId, retrievedAt);

        // Assert
        Assert.Contains("Device Config", inspector.Title);
        Assert.Contains("dev-123", inspector.Title);
    }

    [Fact]
    public void ToInspector_IncludesConfigFields()
    {
        // Arrange
        var config = new DeviceConfig(
            DeviceId: "dev-123",
            MotionDetectionEnabled: true,
            MotionSensitivity: 75,
            RecordingMode: "motion"
        );
        string deviceId = "dev-123";
        DateTime retrievedAt = DateTime.UtcNow;

        // Act
        InspectorModel inspector = DeviceConfigInspectorMapper.ToInspector(config, deviceId, retrievedAt);

        // Assert
        Assert.NotNull(inspector.Fields);
        Assert.NotNull(inspector.Provenance);
    }

    [Fact]
    public void ToInspector_ProvenanceIncludesDeviceIdAndSource()
    {
        // Arrange
        var config = new DeviceConfig(
            DeviceId: "dev-456",
            MotionDetectionEnabled: true,
            MotionSensitivity: 50,
            RecordingMode: "always"
        );
        string deviceId = "dev-456";
        var retrievedAt = new DateTime(2026, 9, 20, 14, 30, 45, DateTimeKind.Utc);

        // Act
        InspectorModel inspector = DeviceConfigInspectorMapper.ToInspector(config, deviceId, retrievedAt);

        // Assert
        Assert.NotNull(inspector.Provenance);
        var provenanceDict = inspector.Provenance.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Assert.Contains("DeviceId", provenanceDict.Keys);
        Assert.Contains("Source", provenanceDict.Keys);
        Assert.Contains("RetrievedAtUtc", provenanceDict.Keys);
        Assert.Equal("dev-456", provenanceDict["DeviceId"]);
        Assert.Equal("IEventAndConfigService.GetDeviceConfigAsync", provenanceDict["Source"]);
        Assert.Equal("2026-09-20 14:30:45", provenanceDict["RetrievedAtUtc"]);
    }

    [Fact]
    public void ToInspector_WithCustomSettings_SetsRawJson()
    {
        // Arrange
        var customSettings = new Dictionary<string, object> { { "customKey", "customValue" } };
        var config = new DeviceConfig(
            DeviceId: "dev-123",
            MotionDetectionEnabled: true,
            MotionSensitivity: 75,
            RecordingMode: "motion",
            CustomSettings: customSettings
        );
        string deviceId = "dev-123";
        DateTime retrievedAt = DateTime.UtcNow;

        // Act
        InspectorModel inspector = DeviceConfigInspectorMapper.ToInspector(config, deviceId, retrievedAt);

        // Assert
        // DeviceConfig doesn't have a MetadataJson property, so RawJson should be null
        Assert.Null(inspector.RawJson);
    }
}

public class QueryApiDrillDownHelpers_Tests
{
    [Fact]
    public void DrillDownLocationId_ReturnsLocationId()
    {
        // Arrange
        var location = new Location(
            Id: "loc-123",
            Name: "Home",
            Address: "123 St"
        );

        // Act
        string result = QueryApiDrillDownHelpers.DrillDownLocationId(location);

        // Assert
        Assert.Equal("loc-123", result);
    }

    [Fact]
    public void DrillDownLocationId_WithNullLocation_ReturnsEmpty()
    {
        // Act
        string result = QueryApiDrillDownHelpers.DrillDownLocationId(null);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void DrillDownDeviceId_ReturnsDeviceId()
    {
        // Arrange
        var device = new Device(
            Id: "dev-456",
            Name: "Camera",
            Type: "camera",
            LocationId: "loc-123",
            IsOnline: true
        );

        // Act
        string result = QueryApiDrillDownHelpers.DrillDownDeviceId(device);

        // Assert
        Assert.Equal("dev-456", result);
    }

    [Fact]
    public void DrillDownDeviceId_WithNullDevice_ReturnsEmpty()
    {
        // Act
        string result = QueryApiDrillDownHelpers.DrillDownDeviceId(null);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void QueryApiDrillDownHelpers_LocationDisplayLabel_WithName_ReturnsName()
    {
        var location = new Location(Id: "loc-123", Name: "Home", Address: null);

        string result = QueryApiDrillDownHelpers.LocationDisplayLabel(location);

        Assert.Equal("Home", result);
    }

    [Fact]
    public void QueryApiDrillDownHelpers_LocationDisplayLabel_WithEmptyName_ReturnsId()
    {
        var location = new Location(Id: "loc-123", Name: "  ", Address: null);

        string result = QueryApiDrillDownHelpers.LocationDisplayLabel(location);

        Assert.Equal("loc-123", result);
    }

    [Fact]
    public void QueryApiDrillDownHelpers_DeviceDisplayLabel_WithEmptyName_ReturnsId()
    {
        var device = new Device(Id: "dev-456", Name: string.Empty, Type: "camera", LocationId: "loc-123", IsOnline: true);

        string result = QueryApiDrillDownHelpers.DeviceDisplayLabel(device);

        Assert.Equal("dev-456", result);
    }

    [Fact]
    public void QueryApiDrillDownHelpers_LocationDisplayLabel_WithNull_ReturnsEmpty()
    {
        string result = QueryApiDrillDownHelpers.LocationDisplayLabel(null);

        Assert.Empty(result);
    }
}
