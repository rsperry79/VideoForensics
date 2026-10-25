namespace VideoForensics.Client.Ui.Shared.Services.Devices;

/// <summary>Whether a device is reachable right now, as shown on an account's Devices tab.</summary>
public enum DeviceUptimeStatus
{
    /// <summary>Last seen within the offline threshold.</summary>
    Online,

    /// <summary>Last seen longer ago than the offline threshold, or the provider reported it offline.</summary>
    Offline,

    /// <summary>No health reading exists, so nothing can be said about the device.</summary>
    Unknown,
}

/// <summary>Turns a device's latest health reading into an uptime status. Pure, so the threshold rule is tested in one place.</summary>
public static class DeviceUptime
{
    /// <summary>
    /// Online when the reading is recent enough. A reading the provider marked offline is offline regardless of its age.
    /// Last seen is the last heartbeat when the provider sent one, otherwise the time the reading was captured.
    /// </summary>
    /// <param name="isOnline">The provider's own online flag from the latest reading, if it sent one.</param>
    /// <param name="lastHeartbeatUtc">Last heartbeat from the provider, if any.</param>
    /// <param name="capturedAtUtc">When the reading was taken, if there is a reading.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="offlineAfterSeconds">How long without a reading before the device counts as offline.</param>
    /// <param name="requiresConnectionFlag">
    /// True when the provider reports connection only through its own flag (Ring's health.connected). A recent reading
    /// with no flag then counts as offline, because recency alone does not show the device is connected.
    /// </param>
    public static DeviceUptimeStatus Evaluate(
        bool? isOnline,
        DateTime? lastHeartbeatUtc,
        DateTime? capturedAtUtc,
        DateTime nowUtc,
        int offlineAfterSeconds,
        bool requiresConnectionFlag = false)
    {
        if (capturedAtUtc is null && lastHeartbeatUtc is null)
        {
            return DeviceUptimeStatus.Unknown;
        }

        if (isOnline == false || (requiresConnectionFlag && isOnline is null))
        {
            return DeviceUptimeStatus.Offline;
        }

        DateTime lastSeenUtc = LastSeenUtc(lastHeartbeatUtc, capturedAtUtc);
        return nowUtc - lastSeenUtc > TimeSpan.FromSeconds(offlineAfterSeconds)
            ? DeviceUptimeStatus.Offline
            : DeviceUptimeStatus.Online;
    }

    /// <summary>
    /// The provider's heartbeat when it sent one, otherwise the capture time. The capture time is not used when a heartbeat
    /// exists: providers such as Ring re-send their last known state on every sync, so a fresh capture can carry a stale heartbeat.
    /// </summary>
    public static DateTime LastSeenUtc(DateTime? lastHeartbeatUtc, DateTime? capturedAtUtc)
    {
        return (lastHeartbeatUtc, capturedAtUtc) switch
        {
            (DateTime heartbeat, _) => heartbeat,
            (null, DateTime captured) => captured,
            _ => DateTime.MinValue,
        };
    }
}
