using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Two cameras can share a name, so every device label carries a piece of its ID.</summary>
public class DeviceLabelTests
{
    [Fact]
    public void LongId_ShowsNameAndLastFourCharacters()
    {
        Assert.Equal("Porch (ID ...4001)", DeviceLabel.For("Porch", "86401464001"));
    }

    [Fact]
    public void ShortId_ShowsWholeId()
    {
        Assert.Equal("Porch (ID ...12)", DeviceLabel.For("Porch", "12"));
    }

    [Fact]
    public void BlankName_FallsBackToIdOnly()
    {
        Assert.Equal("Device ...4001", DeviceLabel.For("  ", "86401464001"));
    }
}
