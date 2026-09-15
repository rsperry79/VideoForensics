using Microsoft.Extensions.Logging;

using System.Text.Json.Nodes;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Providers.Uniview.Services;

/// <summary>
/// Discovers locations and devices on a Uniview NVR. Since a Uniview NVR
/// is a single fixed device (not a cloud account with multiple sites), this
/// service treats the NVR host itself as a synthetic location, and each of
/// its configured channels as a device (camera).
/// </summary>
public class UniviewDeviceDiscoveryService : IDeviceDiscoveryService
{
    private readonly ILogger<UniviewDeviceDiscoveryService> _logger;
    private readonly IUniviewSessionProvider _sessionProvider;
    private readonly IForensicsConfiguration _config;

    public UniviewDeviceDiscoveryService(
        ILogger<UniviewDeviceDiscoveryService> logger,
        IUniviewSessionProvider sessionProvider,
        IForensicsConfiguration config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Returns a single synthetic location representing the Uniview NVR itself.
    /// The location ID is the configured NVR host; the name is derived from the
    /// device's friendly name if available, otherwise defaults to "Uniview NVR".
    /// </summary>
    public async Task<IReadOnlyList<Location>> GetLocationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            UniviewClient? client = _sessionProvider.GetClient();
            if (client == null)
            {
                _logger.LogError("Not authenticated: Uniview client is null");
                return new List<Location>().AsReadOnly();
            }

            string? nvrHost = _config.UniviewNvrHost;
            if (string.IsNullOrEmpty(nvrHost))
            {
                _logger.LogError("Uniview NVR host not configured");
                return new List<Location>().AsReadOnly();
            }

            // Try to get a friendly device name from GetDeviceInfoAsync
            string locationName = "Uniview NVR";
            try
            {
                JsonNode? deviceInfo = await client.GetDeviceInfoAsync(cancellationToken);
                string? friendlyName = deviceInfo?["DeviceName"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(friendlyName))
                {
                    locationName = friendlyName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch device info for friendly name; using default");
            }

            var location = new Location(
                Id: nvrHost,
                Name: locationName
            );

            _logger.LogInformation("Uniview location: {LocationId} - {LocationName}", location.Id, location.Name);

            return new List<Location> { location }.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Uniview locations");
            throw;
        }
    }

    /// <summary>
    /// Returns all devices (channels) at the given location. If the location ID
    /// does not match the configured NVR host, returns an empty list.
    /// </summary>
    public async Task<IReadOnlyList<Device>> GetDevicesAsync(string locationId, CancellationToken cancellationToken = default)
    {
        try
        {
            UniviewClient? client = _sessionProvider.GetClient();
            if (client == null)
            {
                _logger.LogError("Not authenticated: Uniview client is null");
                return new List<Device>().AsReadOnly();
            }

            string? nvrHost = _config.UniviewNvrHost;
            if (string.IsNullOrEmpty(nvrHost))
            {
                _logger.LogError("Uniview NVR host not configured");
                return new List<Device>().AsReadOnly();
            }

            // Only return devices if the location ID matches the configured NVR host
            if (locationId != nvrHost)
            {
                _logger.LogInformation("Location {LocationId} does not match configured NVR host {NvrHost}; returning empty device list",
                    locationId, nvrHost);
                return new List<Device>().AsReadOnly();
            }

            IReadOnlyList<UniviewClient.ChannelInfo> channels = await client.GetChannelListAsync(cancellationToken);
            var devices = new List<Device>();

            foreach (UniviewClient.ChannelInfo channel in channels)
            {
                var device = new Device(
                    Id: channel.Index.ToString(),
                    Name: channel.Name,
                    Type: "camera",
                    LocationId: nvrHost,
                    IsOnline: channel.IsOnline
                );
                devices.Add(device);
            }

            _logger.LogInformation("Found {DeviceCount} channels at Uniview NVR {NvrHost}", devices.Count, nvrHost);
            foreach (Device device in devices)
            {
                _logger.LogInformation("  Device: {DeviceId} - {DeviceName}, Online: {IsOnline}",
                    device.Id, device.Name, device.IsOnline);
            }

            return devices.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching devices for location {LocationId}", locationId);
            throw;
        }
    }

    /// <summary>
    /// Returns a specific device (channel) by ID. The device ID is expected to be
    /// the channel number as a string. If not found, returns null.
    /// </summary>
    public async Task<Device?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching device: {DeviceId}", deviceId);

            UniviewClient? client = _sessionProvider.GetClient();
            if (client == null)
            {
                _logger.LogError("Not authenticated: Uniview client is null");
                return null;
            }

            string? nvrHost = _config.UniviewNvrHost;
            if (string.IsNullOrEmpty(nvrHost))
            {
                _logger.LogError("Uniview NVR host not configured");
                return null;
            }

            // Parse deviceId as channel number
            if (!int.TryParse(deviceId, out int channelNumber))
            {
                _logger.LogWarning("Invalid device ID format: {DeviceId}", deviceId);
                return null;
            }

            // Get all devices and find the matching one
            IReadOnlyList<Device> devices = await GetDevicesAsync(nvrHost, cancellationToken);
            Device? device = devices.FirstOrDefault(d => d.Id == deviceId);

            if (device != null)
            {
                _logger.LogInformation("Found device: {DeviceId} - {DeviceName}", device.Id, device.Name);
            }
            else
            {
                _logger.LogInformation("Device not found: {DeviceId}", deviceId);
            }

            return device;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching device {DeviceId}", deviceId);
            return null;
        }
    }
}
