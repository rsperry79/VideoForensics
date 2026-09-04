namespace VideoForensics.Hosting
{
    /// <summary>Coarse power-source state, used to slow down background polling while on battery.</summary>
    public enum BatteryStatus
    {
        Unknown,
        OnAcPower,
        OnBattery
    }

    /// <summary>
    /// Reports the current host's power-source state. Every server-tier host (console, MCP,
    /// VideoForensics.WebApp) registers the no-op AlwaysOnAcPower implementation below - only a
    /// MAUI client would ever have a real answer here (via Microsoft.Maui.Devices.Battery), and
    /// per the "only the server pulls new media/telemetry" rule, MAUI never runs
    /// DeviceHealthSyncService itself, so no MAUI implementation exists yet. The abstraction stays
    /// host-agnostic regardless, for whatever legitimately does run on a battery-powered host later.
    /// </summary>
    public interface IBatteryStatusProvider
    {
        BatteryStatus GetStatus();
    }

    /// <summary>Default implementation for hosts that are always on mains power (console, MCP, WebApp).</summary>
    public class AlwaysOnAcPower : IBatteryStatusProvider
    {
        public BatteryStatus GetStatus() => BatteryStatus.OnAcPower;
    }
}
