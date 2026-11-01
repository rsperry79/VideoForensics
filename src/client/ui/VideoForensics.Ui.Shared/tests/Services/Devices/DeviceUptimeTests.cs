using VideoForensics.Client.Ui.Shared.Services.Devices;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Devices;

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
    public void NoConnectionFlag_WhenProviderRequiresIt_IsOfflineEvenWhenRecent()
    {
        // Ring reports connection only through health.connected. A recent capture without that flag is not proof of connection.
        Assert.Equal(DeviceUptimeStatus.Offline, DeviceUptime.Evaluate(null, Now.AddSeconds(-10), Now.AddSeconds(-10), Now, 300, requiresConnectionFlag: true));
    }

    [Fact]
    public void NoConnectionFlag_WhenProviderDoesNotRequireIt_StillUsesRecency()
    {
        Assert.Equal(DeviceUptimeStatus.Online, DeviceUptime.Evaluate(null, null, Now.AddSeconds(-10), Now, 300));
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
    public void StaleHeartbeat_WithFreshConnectedCapture_IsOnline()
    {
        // Ring re-sends its last heartbeat on every sync, so a year-old heartbeat beside a fresh health.connected=true poll is online.
        Assert.Equal(DeviceUptimeStatus.Online, DeviceUptime.Evaluate(true, Now.AddDays(-365), Now.AddSeconds(-10), Now, 300, requiresConnectionFlag: true));
    }

    [Fact]
    public void ConnectedCaptureOlderThanThreshold_IsOffline_EvenWithFreshHeartbeat()
    {
        // A connected reading only counts while the poll that produced it is recent.
        Assert.Equal(DeviceUptimeStatus.Offline, DeviceUptime.Evaluate(true, Now.AddSeconds(-10), Now.AddSeconds(-301), Now, 300, requiresConnectionFlag: true));
    }

    [Fact]
    public void ConnectionFlagFalse_IsOfflineEvenWithFreshCapture_WhenProviderRequiresIt()
    {
        Assert.Equal(DeviceUptimeStatus.Offline, DeviceUptime.Evaluate(false, Now.AddSeconds(-10), Now.AddSeconds(-10), Now, 300, requiresConnectionFlag: true));
    }

    [Fact]
    public void LastSeen_StaleHeartbeat_IsNotReplacedByCapture()
    {
        DateTime heartbeat = Now.AddDays(-365);

        Assert.Equal(heartbeat, DeviceUptime.LastSeenUtc(heartbeat, Now));
    }

    [Fact]
    public void LastSeen_UsesHeartbeatWhenPresent_AndCaptureOtherwise()
    {
        DateTime heartbeat = Now.AddSeconds(-10);
        DateTime captured = Now.AddSeconds(-30);

        Assert.Equal(heartbeat, DeviceUptime.LastSeenUtc(heartbeat, captured));
        Assert.Equal(captured, DeviceUptime.LastSeenUtc(null, captured));
        Assert.Equal(heartbeat, DeviceUptime.LastSeenUtc(heartbeat, null));
    }
}
