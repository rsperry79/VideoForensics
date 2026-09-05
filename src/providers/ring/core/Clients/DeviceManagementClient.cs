using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Interfaces;

namespace VideoForensics.Providers.Ring.Clients;

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

    public async Task<List<Doorbot>> GetAllDevicesAsync(CancellationToken cancellationToken = default)
    {
        return await _discoveryService.GetRingDevices(null, cancellationToken);
    }

    public async Task<Doorbot> GetDeviceByNameAsync(string deviceName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(deviceName))
        {
            throw new ArgumentException("Device name is required", nameof(deviceName));
        }

        List<Doorbot> devices = await _discoveryService.GetRingDevices(null, cancellationToken);
        Doorbot? device = devices.FirstOrDefault(d =>
            d.Description?.Equals(deviceName, StringComparison.OrdinalIgnoreCase) ?? false);

        return device == null ? throw new KeyNotFoundException($"Device '{deviceName}' not found") : device;
    }

    public async Task<Doorbot> GetDeviceByIdAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            throw new ArgumentException("Device ID is required", nameof(deviceId));
        }

        List<Doorbot> devices = await _discoveryService.GetRingDevices(null, cancellationToken);
        Doorbot? device = devices.FirstOrDefault(d => d.DeviceId == deviceId);

        return device == null ? throw new KeyNotFoundException($"Device '{deviceId}' not found") : device;
    }

    public async Task<bool> ControlDeviceAsync(string deviceId, DeviceAction action, CancellationToken cancellationToken = default)
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
                "light_on" => await _controlService.SetLight(deviceId, true, cancellationToken),
                "light_off" => await _controlService.SetLight(deviceId, false, cancellationToken),
                "siren_on" => await _controlService.SetSiren(deviceId, true, action.Parameters.ContainsKey("duration") ? (int)action.Parameters["duration"] : 30, cancellationToken),
                "siren_off" => await _controlService.SetSiren(deviceId, false, cancellationToken: cancellationToken),
                "night_mode_on" => await _controlService.SetNightMode(deviceId, true, cancellationToken),
                "night_mode_off" => await _controlService.SetNightMode(deviceId, false, cancellationToken),
                "motion_detection_on" => await _controlService.SetMotionDetection(deviceId, true, cancellationToken),
                "motion_detection_off" => await _controlService.SetMotionDetection(deviceId, false, cancellationToken),
                _ => throw new NotSupportedException($"Action '{action.ActionType}' is not supported")
            };
        }
        catch
        {
            return false;
        }
    }

    public async Task<DeviceStatusInfo> GetDeviceStatusAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            throw new ArgumentException("Device ID is required", nameof(deviceId));
        }

        Doorbot device = await GetDeviceByIdAsync(deviceId, cancellationToken);

        _ = await _healthService.GetDoorbotHealth(deviceId, cancellationToken);

        return new DeviceStatusInfo
        {
            DeviceId = deviceId,
            IsOnline = device.ExternalConnection ?? false,
            BatteryLevel = device.BatteryLife,
            LastSeen = DateTime.UtcNow
        };
    }

    public async Task<List<Location>> GetAllLocationsAsync(CancellationToken cancellationToken = default)
    {
        return await _discoveryService.GetLocations(cancellationToken);
    }

    public async Task<List<Doorbot>> GetDevicesByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        return locationId == Guid.Empty
            ? throw new ArgumentException("Location ID is required", nameof(locationId))
            : await _discoveryService.GetRingDevices(locationId, cancellationToken);
    }

    public async Task<bool> SetLocationModeAsync(Guid locationId, string mode, CancellationToken cancellationToken = default)
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
        return await _locationService.SetLocationMode(locationId, locationMode, cancellationToken);
    }
}
