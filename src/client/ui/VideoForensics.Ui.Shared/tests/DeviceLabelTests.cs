using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Two cameras can share a name, so every device label carries its full ID.</summary>
public class DeviceLabelTests
{
    [Fact]
    public void LongId_ShowsNameAndFullId()
    {
        Assert.Equal("Porch (ID 86401464001)", DeviceLabel.For("Porch", "86401464001"));
    }

    [Fact]
    public void ShortId_ShowsNameAndWholeId()
    {
        Assert.Equal("Porch (ID 12)", DeviceLabel.For("Porch", "12"));
    }

    [Fact]
    public void BlankName_FallsBackToFullIdOnly()
    {
        Assert.Equal("Device 86401464001", DeviceLabel.For("  ", "86401464001"));
    }
}
