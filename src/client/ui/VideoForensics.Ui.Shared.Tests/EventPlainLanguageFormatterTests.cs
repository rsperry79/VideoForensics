using Xunit;
using VideoForensics.Api.Contracts;
using VideoForensics.Ui.Shared.Formatting;

namespace VideoForensics.Ui.Shared.Tests;

public class EventPlainLanguageFormatterTests
{
    [Fact]
    public void EventPlainLanguageFormatter_Describe_MotionEvent_ReturnsPlainLanguageDescription()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var eventDto = new EventDto(
            Id: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            ProviderEventId: "provider-123",
            EventType: "motion",
            OccurredAtUtc: now,
            SnapshotUrl: null,
            MetadataJson: null,
            DiscoveredAtUtc: now,
            DownloadedAtUtc: null,
            ApiSourceHash: null,
            EventIntegrityHash: null
        );
        var deviceName = "Front Door Camera";

        // Act
        string result = EventPlainLanguageFormatter.Describe(TestLocalizer.Create(), eventDto, deviceName);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("motion", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Front Door Camera", result);
    }

    [Fact]
    public void EventPlainLanguageFormatter_Describe_PersonEvent_ReturnsPlainLanguageDescription()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var eventDto = new EventDto(
            Id: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            ProviderEventId: "provider-456",
            EventType: "person",
            OccurredAtUtc: now,
            SnapshotUrl: null,
            MetadataJson: null,
            DiscoveredAtUtc: now,
            DownloadedAtUtc: null,
            ApiSourceHash: null,
            EventIntegrityHash: null
        );
        var deviceName = "Side Yard Camera";

        // Act
        string result = EventPlainLanguageFormatter.Describe(TestLocalizer.Create(), eventDto, deviceName);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("person", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Side Yard Camera", result);
    }

    [Fact]
    public void EventPlainLanguageFormatter_Describe_PackageEvent_ReturnsPlainLanguageDescription()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var eventDto = new EventDto(
            Id: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            ProviderEventId: "provider-789",
            EventType: "package",
            OccurredAtUtc: now,
            SnapshotUrl: null,
            MetadataJson: null,
            DiscoveredAtUtc: now,
            DownloadedAtUtc: null,
            ApiSourceHash: null,
            EventIntegrityHash: null
        );
        var deviceName = "Porch Camera";

        // Act
        string result = EventPlainLanguageFormatter.Describe(TestLocalizer.Create(), eventDto, deviceName);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("package", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Porch Camera", result);
    }

    [Fact]
    public void EventPlainLanguageFormatter_Describe_UnknownEventType_ReturnsSafeFallback()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var eventDto = new EventDto(
            Id: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            ProviderEventId: "provider-unknown",
            EventType: "unknown_event_type",
            OccurredAtUtc: now,
            SnapshotUrl: null,
            MetadataJson: null,
            DiscoveredAtUtc: now,
            DownloadedAtUtc: null,
            ApiSourceHash: null,
            EventIntegrityHash: null
        );
        var deviceName = "My Device";

        // Act
        string result = EventPlainLanguageFormatter.Describe(TestLocalizer.Create(), eventDto, deviceName);

        // Assert - should not throw, should return a safe generic message
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("activity", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EventPlainLanguageFormatter_Describe_UnknownEventType_NeverShowsRawEventType()
    {
        var now = DateTime.UtcNow;
        var eventDto = new EventDto(
            Id: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            ProviderEventId: "provider-unknown",
            EventType: "ding_dong_v2",
            OccurredAtUtc: now,
            SnapshotUrl: null,
            MetadataJson: null,
            DiscoveredAtUtc: now,
            DownloadedAtUtc: null,
            ApiSourceHash: null,
            EventIntegrityHash: null
        );

        string result = EventPlainLanguageFormatter.Describe(TestLocalizer.Create(), eventDto, "Porch");

        Assert.DoesNotContain("ding_dong_v2", result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Your Porch detected some activity.", result);
    }

    [Fact]
    public void EventPlainLanguageFormatter_Describe_NullDeviceName_StillReturnsValidDescription()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var eventDto = new EventDto(
            Id: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            ProviderEventId: "provider-123",
            EventType: "motion",
            OccurredAtUtc: now,
            SnapshotUrl: null,
            MetadataJson: null,
            DiscoveredAtUtc: now,
            DownloadedAtUtc: null,
            ApiSourceHash: null,
            EventIntegrityHash: null
        );

        // Act
        string result = EventPlainLanguageFormatter.Describe(TestLocalizer.Create(), eventDto, null);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("motion", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EventPlainLanguageFormatter_DescribeJammingIncident_JammingEvent_ReturnsPlainLanguageDescription()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-1);
        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;
        var deviceName = "Front Door Camera";

        // Act
        string result = EventPlainLanguageFormatter.DescribeJammingIncident(TestLocalizer.Create(), 
            deviceName,
            startTime,
            endTime,
            -72,
            12,
            "High"
        );

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("blocked", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Front Door Camera", result);
    }

    [Fact]
    public void EventPlainLanguageFormatter_DescribeJammingIncident_ShortDuration_ReturnsMinutes()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var endTime = startTime.AddMinutes(15);
        var deviceName = "Camera";

        // Act
        string result = EventPlainLanguageFormatter.DescribeJammingIncident(TestLocalizer.Create(), 
            deviceName,
            startTime,
            endTime,
            -85,
            30,
            "Medium"
        );

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("15 minute", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EventPlainLanguageFormatter_DescribeJammingIncident_LongDuration_ReturnsHours()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var endTime = startTime.AddHours(3).AddMinutes(30);
        var deviceName = "Camera";

        // Act
        string result = EventPlainLanguageFormatter.DescribeJammingIncident(TestLocalizer.Create(), 
            deviceName,
            startTime,
            endTime,
            -90,
            50,
            "Definite"
        );

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("3 hour", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EventPlainLanguageFormatter_DescribeJammingIncident_VeryShortDuration_ReturnSeconds()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var endTime = startTime.AddSeconds(45);
        var deviceName = "Camera";

        // Act
        string result = EventPlainLanguageFormatter.DescribeJammingIncident(TestLocalizer.Create(), 
            deviceName,
            startTime,
            endTime,
            -65,
            10,
            "Low"
        );

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("45 second", result, StringComparison.OrdinalIgnoreCase);
    }
}
