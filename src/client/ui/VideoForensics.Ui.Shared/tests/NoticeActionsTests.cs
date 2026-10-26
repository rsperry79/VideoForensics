using VideoForensics.Client.Ui.Shared.Services.Notices;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The notice buttons: a case from a signal deviation, and the device settings link for an offline camera.</summary>
public class NoticeActionsTests
{
    private const string Path = "/signal-history?devices=11111111-2222-3333-4444-555555555555&from=2026-10-26&to=2026-10-27";

    [Fact]
    public void CreateCaseLink_OpensTheNewCaseWithTheNoticesScopeAndDetails()
    {
        string? link = NoticeActions.CreateCaseLink("SignalDeviation", Path, "Front Door: RSSI -56 dBm at Mon Oct 26 00:00 UTC is more than 2 std devs from this camera's mean. [sig-dev:Ring:1234:2026102600]");

        Assert.NotNull(link);
        Assert.StartsWith("/cases/new?devices=11111111-2222-3333-4444-555555555555&from=2026-10-26&to=2026-10-27&title=", link);
        Assert.Contains("&description=" + Uri.EscapeDataString("Front Door: RSSI -56 dBm at Mon Oct 26 00:00 UTC is more than 2 std devs from this camera's mean."), link);
        Assert.DoesNotContain("sig-dev", link);
    }

    [Fact]
    public void CreateCaseLink_IsNullForOtherNotices()
    {
        Assert.Null(NoticeActions.CreateCaseLink("DeviceOffline", Path, "Flood went offline"));
    }

    [Fact]
    public void CreateCaseLink_IsNullWithoutAScopeInTheActionPath()
    {
        Assert.Null(NoticeActions.CreateCaseLink("SignalDeviation", "/signal-history", "details"));
        Assert.Null(NoticeActions.CreateCaseLink("SignalDeviation", null, "details"));
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
}
