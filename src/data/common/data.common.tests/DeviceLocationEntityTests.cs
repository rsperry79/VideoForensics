using VideoForensics.Data.Common.Entities;
using Xunit;

namespace VideoForensics.Data.Common.Tests;

public class DeviceLocationEntityTests
{
    [Fact]
    public void Location_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var providerAccountId = Guid.NewGuid();
        var providerLocationId = "location-123";
        var name = "Front Door";
        var address = "123 Main St";
        var metadataJson = "{\"timezone\": \"UTC\"}";

        // Act
        var location = new Location
        {
            Id = id,
            ProviderAccountId = providerAccountId,
            ProviderLocationId = providerLocationId,
            Name = name,
            Address = address,
            MetadataJson = metadataJson
        };

        // Assert
        Assert.Equal(id, location.Id);
        Assert.Equal(providerAccountId, location.ProviderAccountId);
        Assert.Equal(providerLocationId, location.ProviderLocationId);
        Assert.Equal(name, location.Name);
        Assert.Equal(address, location.Address);
        Assert.Equal(metadataJson, location.MetadataJson);
    }

    [Fact]
    public void Location_WithoutOptionalFields_ReturnsNullValues()
    {
        // Arrange & Act
        var location = new Location
        {
            Id = Guid.NewGuid(),
            ProviderAccountId = Guid.NewGuid(),
            ProviderLocationId = "location-123",
            Name = "Front Door",
            Address = null,
            MetadataJson = null
        };

        // Assert
        Assert.Null(location.Address);
        Assert.Null(location.MetadataJson);
    }

    [Fact]
    public void Device_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var providerDeviceId = "device-456";
        var name = "Front Doorbell";
        var type = "Doorbell";
        var isOnline = true;
        var metadataJson = "{\"model\": \"Ring Doorbell 3\"}";
        var lastSuccessfulPullAtUtc = DateTime.UtcNow.AddHours(-2);
        var lastPullAttemptAtUtc = DateTime.UtcNow.AddHours(-1);
        var timeZoneId = "America/New_York";

        // Act
        var device = new Device
        {
            Id = id,
            LocationId = locationId,
            ProviderDeviceId = providerDeviceId,
            Name = name,
            Type = type,
            IsOnline = isOnline,
            MetadataJson = metadataJson,
            LastSuccessfulPullAtUtc = lastSuccessfulPullAtUtc,
            LastPullAttemptAtUtc = lastPullAttemptAtUtc,
            TimeZoneId = timeZoneId
        };

        // Assert
        Assert.Equal(id, device.Id);
        Assert.Equal(locationId, device.LocationId);
        Assert.Equal(providerDeviceId, device.ProviderDeviceId);
        Assert.Equal(name, device.Name);
        Assert.Equal(type, device.Type);
        Assert.True(device.IsOnline);
        Assert.Equal(metadataJson, device.MetadataJson);
        Assert.Equal(lastSuccessfulPullAtUtc, device.LastSuccessfulPullAtUtc);
        Assert.Equal(lastPullAttemptAtUtc, device.LastPullAttemptAtUtc);
        Assert.Equal(timeZoneId, device.TimeZoneId);
    }

    [Fact]
    public void Device_WithoutOptionalFields_ReturnsNullValues()
    {
        // Arrange & Act
        var device = new Device
        {
            Id = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            ProviderDeviceId = "device-456",
            Name = "Front Doorbell",
            Type = "Doorbell",
            IsOnline = false,
            MetadataJson = null,
            LastSuccessfulPullAtUtc = null,
            LastPullAttemptAtUtc = null,
            TimeZoneId = null
        };

        // Assert
        Assert.Null(device.MetadataJson);
        Assert.Null(device.LastSuccessfulPullAtUtc);
        Assert.Null(device.LastPullAttemptAtUtc);
        Assert.Null(device.TimeZoneId);
        Assert.False(device.IsOnline);
    }

    [Fact]
    public void Device_WithPartialTimestamps_RoundsTrip()
    {
        // Arrange
        var lastSuccessfulPullAtUtc = DateTime.UtcNow.AddDays(-1);

        // Act
        var device = new Device
        {
            Id = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            ProviderDeviceId = "device-456",
            Name = "Front Doorbell",
            Type = "Doorbell",
            IsOnline = true,
            MetadataJson = null,
            LastSuccessfulPullAtUtc = lastSuccessfulPullAtUtc,
            LastPullAttemptAtUtc = null,
            TimeZoneId = null
        };

        // Assert
        Assert.Equal(lastSuccessfulPullAtUtc, device.LastSuccessfulPullAtUtc);
        Assert.Null(device.LastPullAttemptAtUtc);
    }
}
