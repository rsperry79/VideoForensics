using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Interfaces;
using KoenZomers.Ring.Api.Entities;

namespace KoenZomers.Ring.Api.Clients;

/// <summary>
/// High-level client for managing Ring devices and locations.
/// </summary>
public class DeviceManagementClient : IDeviceManagementClient
{
    private readonly IDeviceDiscoveryService _discoveryService;
    private readonly IDeviceControlService _controlService;
    private readonly IHealthMonitoringService _healthService;
    private readonly ILocationManagementService _locationService;

    public DeviceManagementClient(
        IDeviceDiscoveryService discoveryService,
        IDeviceControlService controlService,
        IHealthMonitoringService healthService,
        ILocationManagementService locationService)
    {
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _controlService = controlService ?? throw new ArgumentNullException(nameof(controlService));
        _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
        _locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
    }

    public async Task<List<Doorbot>> GetAllDevicesAsync()
    {
        return await _discoveryService.GetRingDevices();
    }

    public async Task<Doorbot> GetDeviceByNameAsync(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName))
        {
            throw new ArgumentException("Device name is required", nameof(deviceName));
        }

        var devices = await _discoveryService.GetRingDevices();
        var device = devices.FirstOrDefault(d =>
            d.Description?.Equals(deviceName, StringComparison.OrdinalIgnoreCase) ?? false);

        if (device == null)
        {
            throw new KeyNotFoundException($"Device '{deviceName}' not found");
        }

        return device;
    }

    public async Task<Doorbot> GetDeviceByIdAsync(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            throw new ArgumentException("Device ID is required", nameof(deviceId));
        }

        var devices = await _discoveryService.GetRingDevices();
        var device = devices.FirstOrDefault(d => d.DeviceId == deviceId);

        if (device == null)
        {
            throw new KeyNotFoundException($"Device '{deviceId}' not found");
        }

        return device;
    }

    public async Task<bool> ControlDeviceAsync(string deviceId, DeviceAction action)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            throw new ArgumentException("Device ID is required", nameof(deviceId));
        }

        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        try
        {
            return action.ActionType.ToLower() switch
            {
                "light_on" => await _controlService.SetLight(deviceId, true),
                "light_off" => await _controlService.SetLight(deviceId, false),
                "siren_on" => await _controlService.SetSiren(deviceId, true, action.Parameters.ContainsKey("duration") ? (int)action.Parameters["duration"] : 30),
                "siren_off" => await _controlService.SetSiren(deviceId, false),
                "night_mode_on" => await _controlService.SetNightMode(deviceId, true),
                "night_mode_off" => await _controlService.SetNightMode(deviceId, false),
                "motion_detection_on" => await _controlService.SetMotionDetection(deviceId, true),
                "motion_detection_off" => await _controlService.SetMotionDetection(deviceId, false),
                _ => throw new NotSupportedException($"Action '{action.ActionType}' is not supported")
            };
        }
        catch
        {
            return false;
        }
    }

    public async Task<DeviceStatusInfo> GetDeviceStatusAsync(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            throw new ArgumentException("Device ID is required", nameof(deviceId));
        }

        var device = await GetDeviceByIdAsync(deviceId);
        var health = await _healthService.GetDoorbotHealth(deviceId);

        return new DeviceStatusInfo
        {
            DeviceId = deviceId,
            IsOnline = device.ExternalConnection ?? false,
            BatteryLevel = device.BatteryLife,
            LastSeen = DateTime.UtcNow
        };
    }

    public async Task<List<Location>> GetAllLocationsAsync()
    {
        return await _discoveryService.GetLocations();
    }

    public async Task<List<Doorbot>> GetDevicesByLocationAsync(Guid locationId)
    {
        if (locationId == Guid.Empty)
        {
            throw new ArgumentException("Location ID is required", nameof(locationId));
        }

        return await _discoveryService.GetRingDevices(locationId);
    }

    public async Task<bool> SetLocationModeAsync(Guid locationId, string mode)
    {
        if (locationId == Guid.Empty)
        {
            throw new ArgumentException("Location ID is required", nameof(locationId));
        }

        if (string.IsNullOrEmpty(mode))
        {
            throw new ArgumentException("Mode is required", nameof(mode));
        }

        var locationMode = new LocationMode { Mode = mode };
        return await _locationService.SetLocationMode(locationId, locationMode);
    }
}
