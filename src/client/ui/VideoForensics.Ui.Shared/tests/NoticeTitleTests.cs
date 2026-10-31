using VideoForensics.Client.Ui.Shared.Services.Notices;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Notice titles read as words, and a broken audit chain links to the page that shows it.</summary>
public class NoticeTitleTests
{
    [Theory]
    [InlineData("SignalDeviation", "Signal Deviation")]
    [InlineData("ChainOfCustodyIntegrityFailed", "Chain Of Custody Integrity Failed")]
    [InlineData("DeviceOffline", "Device Offline")]
    [InlineData("Alert", "Alert")]
    [InlineData("", "")]
    public void Title_PutsSpacesBetweenTheWords(string eventType, string expected)
    {
        Assert.Equal(expected, NoticeActions.Title(eventType));
    }

    [Fact]
    public void ReviewLink_IsTheChainOfCustodyPageForAChainFailure()
    {
        Assert.Equal("/analyze/chain-of-custody", NoticeActions.ReviewLink("ChainOfCustodyIntegrityFailed", "/analyze/chain-of-custody"));
    }

    [Fact]
    public void ReviewLink_IsNullForOtherNoticesOrWithoutAPath()
    {
        Assert.Null(NoticeActions.ReviewLink("DeviceOffline", "/accounts/abc/sync"));
        Assert.Null(NoticeActions.ReviewLink("ChainOfCustodyIntegrityFailed", null));
    }
}
