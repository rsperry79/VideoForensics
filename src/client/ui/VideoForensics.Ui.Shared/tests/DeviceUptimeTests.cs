using VideoForensics.Client.Ui.Shared.Services.Devices;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class DeviceUptimeTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoReading_IsUnknown()
    {
        Assert.Equal(DeviceUptimeStatus.Unknown, DeviceUptime.Evaluate(null, null, null, Now, 300));
    }

    [Fact]
    public void SeenWithinThreshold_IsOnline()
    {
        Assert.Equal(DeviceUptimeStatus.Online, DeviceUptime.Evaluate(null, null, Now.AddSeconds(-299), Now, 300));
    }

    [Fact]
    public void SeenExactlyAtThreshold_IsStillOnline()
    {
        // Offline means strictly longer than the threshold.
        Assert.Equal(DeviceUptimeStatus.Online, DeviceUptime.Evaluate(null, null, Now.AddSeconds(-300), Now, 300));
    }

    [Fact]
    public void SeenLongerAgoThanThreshold_IsOffline()
    {
        Assert.Equal(DeviceUptimeStatus.Offline, DeviceUptime.Evaluate(null, null, Now.AddSeconds(-301), Now, 300));
    }

    [Fact]
    public void ProviderReportedOffline_IsOfflineEvenWhenRecent()
    {
        Assert.Equal(DeviceUptimeStatus.Offline, DeviceUptime.Evaluate(false, null, Now, Now, 300));
    }

    [Fact]
    public void HeartbeatIsNewerThanCapture_HeartbeatCountsAsLastSeen()
    {
        // The capture is old, but a heartbeat a minute ago means the device is online.
        Assert.Equal(DeviceUptimeStatus.Online, DeviceUptime.Evaluate(null, Now.AddSeconds(-60), Now.AddSeconds(-900), Now, 300));
    }

    [Fact]
    public void OnlyHeartbeat_IsUsedAsLastSeen()
    {
        Assert.Equal(DeviceUptimeStatus.Offline, DeviceUptime.Evaluate(null, Now.AddSeconds(-600), null, Now, 300));
    }

    [Fact]
    public void LastSeen_PicksTheNewerOfHeartbeatAndCapture()
    {
        DateTime heartbeat = Now.AddSeconds(-10);
        DateTime captured = Now.AddSeconds(-30);

        Assert.Equal(heartbeat, DeviceUptime.LastSeenUtc(heartbeat, captured));
        Assert.Equal(captured, DeviceUptime.LastSeenUtc(null, captured));
        Assert.Equal(heartbeat, DeviceUptime.LastSeenUtc(heartbeat, null));
    }
}
