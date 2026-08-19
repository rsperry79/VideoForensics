using System;
using System.Collections.Generic;
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
    Task<DeviceHealth> GetDoorbotHealth(string doorbotId);

    /// <summary>
    /// Gets the health status of a chime device.
    /// </summary>
    Task<DeviceHealth> GetChimeHealth(string chimeId);

    /// <summary>
    /// Gets the monitoring status for a location.
    /// </summary>
    Task<Dictionary<string, object>> GetMonitoringStatus(Guid locationId);

    /// <summary>
    /// Gets detailed device health information including battery and connectivity.
    /// </summary>
    Task<DeviceHealthResponse> GetDetailedDeviceHealth(string deviceId);
}
