#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Entities;

namespace KoenZomers.Ring.Api.Interfaces;

/// <summary>
/// Service for monitoring the health and status of Ring devices.
/// </summary>
public interface IHealthMonitoringService
{
    /// <summary>
    /// Gets the health status of a doorbot device.
    /// </summary>
    Task<DeviceHealth> GetDoorbotHealth(string doorbotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the health status of a chime device.
    /// </summary>
    Task<DeviceHealth> GetChimeHealth(string chimeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the monitoring status for a location. Returns a JsonElement representing the status structure.
    /// </summary>
    Task<System.Text.Json.JsonElement> GetMonitoringStatus(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed device health information including battery and connectivity.
    /// </summary>
    Task<DeviceHealthResponse> GetDetailedDeviceHealth(string deviceId, CancellationToken cancellationToken = default);
}
