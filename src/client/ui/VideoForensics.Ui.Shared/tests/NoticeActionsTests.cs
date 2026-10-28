using VideoForensics.Client.Ui.Shared.Services.Notices;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The notice buttons: a case from a signal deviation, and the device settings link for an offline camera.</summary>
public class NoticeActionsTests
{
    private const string Path = "/signal-history?devices=11111111-2222-3333-4444-555555555555&from=2026-10-26&to=2026-10-27";

    [Fact]
    public void ReviewLink_ForSignalDeviation_IsTheSignalHistoryPathDrilledToTheIncident()
    {
        Assert.Equal(Path, NoticeActions.ReviewLink("SignalDeviation", Path));
    }

    [Fact]
    public void ReviewLink_ForSignalDeviationWithoutAPath_IsNull()
    {
        Assert.Null(NoticeActions.ReviewLink("SignalDeviation", null));
        Assert.Null(NoticeActions.ReviewLink("SignalDeviation", "  "));
    }

    [Fact]
    public void DeviceSettingsLink_IsTheOfflineNoticesActionPath()
    {
        Assert.Equal("/accounts/abc/sync", NoticeActions.DeviceSettingsLink("DeviceOffline", "/accounts/abc/sync"));
    }

    [Fact]
    public void DeviceSettingsLink_IsNullForOtherNoticesOrWithoutAPath()
    {
        Assert.Null(NoticeActions.DeviceSettingsLink("SignalDeviation", Path));
        Assert.Null(NoticeActions.DeviceSettingsLink("DeviceOffline", null));
        Assert.Null(NoticeActions.DeviceSettingsLink("DeviceOffline", "  "));
    }

    [Fact]
    public void ShowsOpenLink_IsFalseWhenReviewOrDeviceSettingsAlreadyOpensThePath()
    {
        Assert.False(NoticeActions.ShowsOpenLink(NoticeActions.SignalDeviationEventType, Path));
        Assert.False(NoticeActions.ShowsOpenLink(NoticeActions.ChainOfCustodyFailedEventType, Path));
        Assert.False(NoticeActions.ShowsOpenLink(NoticeActions.DeviceOfflineEventType, Path));
    }

    [Fact]
    public void ShowsOpenLink_IsTrueForANoticeWithAPathAndNoOtherLink()
    {
        Assert.True(NoticeActions.ShowsOpenLink("SomethingElse", Path));
    }

    [Fact]
    public void ShowsOpenLink_IsFalseWithoutAPath()
    {
        Assert.False(NoticeActions.ShowsOpenLink("SomethingElse", null));
    }
}
