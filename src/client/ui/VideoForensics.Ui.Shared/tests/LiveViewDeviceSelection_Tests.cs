using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Which camera the Live view page starts with when it is opened from the review page.</summary>
public class LiveViewDeviceSelection_Tests
{
    private static Device NewDevice()
    {
        return new Device { Id = Guid.NewGuid(), ProviderDeviceId = "1", Name = "Door", Type = "camera", LocationId = Guid.NewGuid() };
    }

    [Fact]
    public void Initial_RequestedCameraExists_ReturnsItsId()
    {
        // Arrange
        Device wanted = NewDevice();
        Device[] devices = [NewDevice(), wanted];

        // Act
        string? initial = LiveViewDeviceSelection.Initial(devices, wanted.Id);

        // Assert
        Assert.Equal(wanted.Id.ToString(), initial);
    }

    [Fact]
    public void Initial_RequestedCameraUnknown_ReturnsNull()
    {
        Assert.Null(LiveViewDeviceSelection.Initial([NewDevice()], Guid.NewGuid()));
    }

    [Fact]
    public void Initial_NothingRequested_ReturnsNull()
    {
        Assert.Null(LiveViewDeviceSelection.Initial([NewDevice()], null));
    }
}
