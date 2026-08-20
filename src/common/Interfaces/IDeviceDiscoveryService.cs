#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Ring.Api.Entities;

namespace Ring.Api.Interfaces;

/// <summary>
/// Service for discovering and querying Ring devices and locations.
/// </summary>
public interface IDeviceDiscoveryService
{
    /// <summary>
    /// Gets all Ring devices for the authenticated user, optionally filtered by location.
    /// </summary>
    Task<List<Doorbot>> GetRingDevices(Guid? locationId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all locations accessible by the authenticated user.
    /// </summary>
    Task<List<Location>> GetLocations(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets device information by device ID.
    /// </summary>
    Task<Devices> GetDeviceById(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all doorbots (camera devices) in a specific location.
    /// </summary>
    Task<List<Doorbot>> GetDoorbotsInLocation(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the user's profile information.
    /// </summary>
    Task<Profile> GetProfile(CancellationToken cancellationToken = default);
}
