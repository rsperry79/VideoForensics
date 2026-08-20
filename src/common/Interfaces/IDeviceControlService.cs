#nullable enable

using System;
using System.Threading;
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
    Task<bool> SetLight(string doorbotId, bool on, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates or deactivates the siren on a device.
    /// </summary>
    Task<bool> SetSiren(string doorbotId, bool on, int? durationSeconds = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the volume level for a device.
    /// </summary>
    Task<bool> SetVolume(string doorbotId, int volume, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables night mode for a doorbot.
    /// </summary>
    Task<bool> SetNightMode(string doorbotId, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables motion detection for a device.
    /// </summary>
    Task<bool> SetMotionDetection(string doorbotId, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the device settings for a specific device. Returns a JsonElement representing the settings structure.
    /// </summary>
    Task<System.Text.Json.JsonElement> GetDeviceSettings(string doorbotId, CancellationToken cancellationToken = default);
}
