#nullable enable

using System;

namespace VideoForensics.Providers.Ring.Entities;

/// <summary>
/// Represents the current status of a device.
/// </summary>
public class DeviceStatusInfo
{
    /// <summary>
    /// The device ID.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the device is currently online.
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// Battery level as a percentage, if applicable.
    /// </summary>
    public int? BatteryLevel { get; set; }

    /// <summary>
    /// Signal strength indicator.
    /// </summary>
    public string? SignalStrength { get; set; }

    /// <summary>
    /// When the device was last seen online.
    /// </summary>
    public DateTime LastSeen { get; set; }
}
