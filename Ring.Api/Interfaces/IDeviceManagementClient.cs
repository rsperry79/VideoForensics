using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Entities;
using KoenZomers.Ring.Api.Clients;

namespace KoenZomers.Ring.Api.Interfaces;

/// <summary>
/// High-level client for managing Ring devices and locations.
/// </summary>
public interface IDeviceManagementClient
{
    /// <summary>
    /// Gets all devices accessible to the user.
    /// </summary>
    Task<List<Doorbot>> GetAllDevicesAsync();

    /// <summary>
    /// Finds a device by name.
    /// </summary>
    Task<Doorbot> GetDeviceByNameAsync(string deviceName);

    /// <summary>
    /// Gets a device by ID.
    /// </summary>
    Task<Doorbot> GetDeviceByIdAsync(string deviceId);

    /// <summary>
    /// Performs an action on a device (e.g., turn light on/off).
    /// </summary>
    Task<bool> ControlDeviceAsync(string deviceId, DeviceAction action);

    /// <summary>
    /// Gets the current status of a device.
    /// </summary>
    Task<Clients.DeviceStatusInfo> GetDeviceStatusAsync(string deviceId);

    /// <summary>
    /// Gets all locations accessible to the user.
    /// </summary>
    Task<List<Location>> GetAllLocationsAsync();

    /// <summary>
    /// Gets devices in a specific location.
    /// </summary>
    Task<List<Doorbot>> GetDevicesByLocationAsync(Guid locationId);

    /// <summary>
    /// Sets the location mode (Home, Away, Disarmed).
    /// </summary>
    Task<bool> SetLocationModeAsync(Guid locationId, string mode);
}
