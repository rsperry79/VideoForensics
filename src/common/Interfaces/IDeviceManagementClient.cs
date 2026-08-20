#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Entities;

namespace KoenZomers.Ring.Api.Interfaces;

/// <summary>
/// High-level client for managing Ring devices and locations.
/// </summary>
public interface IDeviceManagementClient
{
    /// <summary>
    /// Gets all devices accessible to the user.
    /// </summary>
    Task<List<Doorbot>> GetAllDevicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a device by name.
    /// </summary>
    Task<Doorbot> GetDeviceByNameAsync(string deviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a device by ID.
    /// </summary>
    Task<Doorbot> GetDeviceByIdAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs an action on a device (e.g., turn light on/off).
    /// </summary>
    Task<bool> ControlDeviceAsync(string deviceId, DeviceAction action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a device.
    /// </summary>
    Task<DeviceStatusInfo> GetDeviceStatusAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all locations accessible to the user.
    /// </summary>
    Task<List<Location>> GetAllLocationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets devices in a specific location.
    /// </summary>
    Task<List<Doorbot>> GetDevicesByLocationAsync(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the location mode (Home, Away, Disarmed).
    /// </summary>
    Task<bool> SetLocationModeAsync(Guid locationId, string mode, CancellationToken cancellationToken = default);
}
