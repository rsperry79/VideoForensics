using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Entities;

namespace KoenZomers.Ring.Api.Interfaces;

/// <summary>
/// Service for discovering and querying Ring devices and locations.
/// </summary>
public interface IDeviceDiscoveryService
{
    /// <summary>
    /// Gets all Ring devices for the authenticated user, optionally filtered by location.
    /// </summary>
    Task<List<Doorbot>> GetRingDevices(Guid? locationId = null);

    /// <summary>
    /// Gets all locations accessible by the authenticated user.
    /// </summary>
    Task<List<Location>> GetLocations();

    /// <summary>
    /// Gets device information by device ID.
    /// </summary>
    Task<Devices> GetDeviceById(string deviceId);

    /// <summary>
    /// Gets all doorbots (camera devices) in a specific location.
    /// </summary>
    Task<List<Doorbot>> GetDoorbotsInLocation(Guid locationId);

    /// <summary>
    /// Gets the user's profile information.
    /// </summary>
    Task<Profile> GetProfile();
}
