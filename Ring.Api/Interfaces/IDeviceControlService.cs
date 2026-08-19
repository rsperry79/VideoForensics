using System.Collections.Generic;
using System.Threading.Tasks;

namespace KoenZomers.Ring.Api.Interfaces;

/// <summary>
/// Service for controlling Ring devices.
/// </summary>
public interface IDeviceControlService
{
    /// <summary>
    /// Turns the floodlight on or off for a doorbot.
    /// </summary>
    Task<bool> SetLight(string doorbotId, bool on);

    /// <summary>
    /// Activates or deactivates the siren on a device.
    /// </summary>
    Task<bool> SetSiren(string doorbotId, bool on, int durationSeconds = 30);

    /// <summary>
    /// Sets the volume level for a device.
    /// </summary>
    Task<bool> SetVolume(string doorbotId, int volume);

    /// <summary>
    /// Enables or disables night mode for a doorbot.
    /// </summary>
    Task<bool> SetNightMode(string doorbotId, bool enabled);

    /// <summary>
    /// Enables or disables motion detection for a device.
    /// </summary>
    Task<bool> SetMotionDetection(string doorbotId, bool enabled);

    /// <summary>
    /// Gets the settings for a specific device.
    /// </summary>
    Task<Dictionary<string, object>> GetDeviceSettings(string doorbotId);
}
